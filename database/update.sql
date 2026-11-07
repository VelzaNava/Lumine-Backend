-- Lumine database update - paste into Supabase -> SQL Editor and click Run.
-- Safe to run more than once: every step checks before it changes anything.


-- 1. Catalog columns the API needs

-- Optional unit count shown to the admin (null = not tracked).
alter table public.jewelry add column if not exists stock_quantity integer
    check (stock_quantity is null or stock_quantity >= 0);

-- Up to 3 photos per item, cover first; seed it with each item's existing photo.
alter table public.jewelry add column if not exists image_urls text[] not null default '{}';
update public.jewelry
   set image_urls = array[image_url]
 where coalesce(image_url, '') <> '' and cardinality(image_urls) = 0;


-- 2. One favorite per user + item

delete from public.favorites a
 using public.favorites b
 where a.user_id = b.user_id and a.jewelry_id = b.jewelry_id and a.id > b.id;

create unique index if not exists favorites_user_jewelry on public.favorites (user_id, jewelry_id);


-- 3. Lock the tables: only the Lumine API may read or write them
-- The API uses the service_role key, which bypasses row-level security, so it keeps
-- working. Anyone else (the public anon key, or a user's own login token used against
-- Supabase directly) gets nothing. Without this, all four tables, names and phone
-- numbers included, can be read with the anon key.

alter table public.jewelry       enable row level security;
alter table public.user_profiles enable row level security;
alter table public.favorites     enable row level security;
alter table public.evaluations   enable row level security;

-- Remove any existing policies that opened the tables up.
do $$
declare p record;
begin
  for p in
    select tablename, policyname from pg_policies
     where schemaname = 'public'
       and tablename in ('jewelry', 'user_profiles', 'favorites', 'evaluations')
  loop
    execute format('drop policy %I on public.%I', p.policyname, p.tablename);
  end loop;
end $$;

-- Second lock: no direct table access for the public roles at all.
revoke all on public.jewelry, public.user_profiles, public.favorites, public.evaluations
  from anon, authenticated;

-- The API never calls database functions directly (e.g. the old is_admin()).
revoke execute on all functions in schema public from anon, authenticated;


-- 4. Storage: public to view, API-only to change
-- Product photos and avatars stay viewable by their public URL (the app shows them),
-- but only the API may upload, replace or delete files, and nobody can list a
-- bucket's contents (listing avatars would reveal every user's id).

do $$
declare p record;
begin
  for p in select policyname from pg_policies where schemaname = 'storage' and tablename = 'objects'
  loop
    execute format('drop policy %I on storage.objects', p.policyname);
  end loop;
end $$;

-- The unused "jewelry-assets" bucket accepted any file type up to 50 MB.
-- Keep it private unless you deliberately start using it.
update storage.buckets set public = false where id = 'jewelry-assets';


-- 5. Check the result
-- Every row should say rls_enabled = true and policies = 0.
select c.relname as table_name,
       c.relrowsecurity as rls_enabled,
       (select count(*) from pg_policies p where p.schemaname = 'public' and p.tablename = c.relname) as policies
  from pg_class c
  join pg_namespace n on n.oid = c.relnamespace
 where n.nspname = 'public'
   and c.relname in ('jewelry', 'user_profiles', 'favorites', 'evaluations')
 order by 1;
