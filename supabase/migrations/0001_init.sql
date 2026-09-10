-- =============================================================================
-- Registro de Agencias — Esquema inicial de Supabase (Postgres)
-- Fuente de verdad transaccional. Google Sheets es solo destino sincronizado.
-- =============================================================================

create extension if not exists pgcrypto;

-- -----------------------------------------------------------------------------
-- Trigger genérico: mantener updated_at
-- -----------------------------------------------------------------------------
create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

-- -----------------------------------------------------------------------------
-- Tabla: agencies (datos base importados desde Excel)
-- -----------------------------------------------------------------------------
create table if not exists public.agencies (
  id          uuid primary key default gen_random_uuid(),
  source_key  text not null unique,
  codigo      text not null,
  terminal    text not null,
  grupo       text not null,
  status      text not null default 'PENDING'
              check (status in ('PENDING', 'COMPLETED', 'REVIEW_REQUIRED')),
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now()
);

create index if not exists idx_agencies_grupo         on public.agencies (grupo);
create index if not exists idx_agencies_status        on public.agencies (status);
create index if not exists idx_agencies_grupo_status  on public.agencies (grupo, status);

drop trigger if exists trg_agencies_updated_at on public.agencies;
create trigger trg_agencies_updated_at
  before update on public.agencies
  for each row execute function public.set_updated_at();

-- -----------------------------------------------------------------------------
-- Tabla: agency_profiles (un único perfil por agencia)
-- -----------------------------------------------------------------------------
create table if not exists public.agency_profiles (
  id                        uuid primary key default gen_random_uuid(),
  agency_id                 uuid not null unique
                            references public.agencies (id) on delete restrict,
  direccion                 text not null,
  sector                    text not null,
  municipio                 text not null,
  provincia                 text not null,
  tipo_establecimiento      text not null,
  tipo_establecimiento_otro text,
  latitude                  double precision not null,
  longitude                 double precision not null,
  accuracy_meters           double precision not null,
  location_captured_at      timestamptz not null,
  location_source           text not null default 'browser_geolocation',
  photo_drive_file_id       text,
  photo_url                 text,
  submitted_at              timestamptz not null default now(),
  updated_at                timestamptz not null default now(),
  submitted_by_context      jsonb,
  google_sheet_sync_status  text not null default 'PENDING'
                            check (google_sheet_sync_status in ('PENDING', 'SYNCED', 'FAILED')),
  google_sheet_row_reference text,

  -- Integridad de geolocalización (defensa en la base de datos).
  constraint chk_latitude  check (latitude  >= -90  and latitude  <= 90),
  constraint chk_longitude check (longitude >= -180 and longitude <= 180),
  constraint chk_not_null_island check (not (latitude = 0 and longitude = 0)),
  constraint chk_accuracy  check (accuracy_meters > 0),
  -- Si el tipo es "Otro", el detalle es obligatorio.
  constraint chk_tipo_otro check (
    tipo_establecimiento <> 'Otro'
    or (tipo_establecimiento_otro is not null
        and length(btrim(tipo_establecimiento_otro)) > 0)
  )
);

create index if not exists idx_profiles_sync
  on public.agency_profiles (google_sheet_sync_status);
create index if not exists idx_profiles_agency
  on public.agency_profiles (agency_id);

drop trigger if exists trg_profiles_updated_at on public.agency_profiles;
create trigger trg_profiles_updated_at
  before update on public.agency_profiles
  for each row execute function public.set_updated_at();

-- -----------------------------------------------------------------------------
-- Tabla: audit_log (trazabilidad)
-- -----------------------------------------------------------------------------
create table if not exists public.audit_log (
  id           uuid primary key default gen_random_uuid(),
  entity_type  text not null,
  entity_id    uuid not null,
  action       text not null,
  before_data  jsonb,
  after_data   jsonb,
  actor_email  text,
  created_at   timestamptz not null default now()
);

create index if not exists idx_audit_entity
  on public.audit_log (entity_type, entity_id, created_at desc);

-- -----------------------------------------------------------------------------
-- Seguridad: RLS activado sin políticas.
-- La aplicación accede exclusivamente con la SERVICE ROLE KEY (que omite RLS).
-- La ANON KEY no puede leer ni escribir estas tablas. Nunca expongas datos con
-- la clave anónima.
-- -----------------------------------------------------------------------------
alter table public.agencies        enable row level security;
alter table public.agency_profiles enable row level security;
alter table public.audit_log       enable row level security;

-- =============================================================================
-- RPC transaccional: crear perfil y completar agencia de forma atómica.
-- Garantiza exclusión mutua real ("una agencia se completa una sola vez")
-- mediante SELECT ... FOR UPDATE + verificación de estado + UNIQUE(agency_id).
-- =============================================================================
create or replace function public.submit_agency_profile(
  p_agency_id                 uuid,
  p_direccion                 text,
  p_sector                    text,
  p_municipio                 text,
  p_provincia                 text,
  p_tipo_establecimiento      text,
  p_tipo_establecimiento_otro text,
  p_latitude                  double precision,
  p_longitude                 double precision,
  p_accuracy_meters           double precision,
  p_location_captured_at      timestamptz,
  p_location_source           text,
  p_context                   jsonb,
  p_actor                     text
)
returns public.agency_profiles
language plpgsql
security definer
set search_path = public
as $$
declare
  v_agency  public.agencies;
  v_profile public.agency_profiles;
begin
  -- Bloquea la fila de la agencia para serializar envíos concurrentes.
  select * into v_agency from public.agencies where id = p_agency_id for update;

  if not found then
    raise exception 'AGENCY_NOT_FOUND' using errcode = 'P0002';
  end if;

  if v_agency.status <> 'PENDING' then
    raise exception 'AGENCY_NOT_PENDING' using errcode = 'P0001';
  end if;

  insert into public.agency_profiles (
    agency_id, direccion, sector, municipio, provincia,
    tipo_establecimiento, tipo_establecimiento_otro,
    latitude, longitude, accuracy_meters, location_captured_at, location_source,
    submitted_by_context, google_sheet_sync_status
  ) values (
    p_agency_id, p_direccion, p_sector, p_municipio, p_provincia,
    p_tipo_establecimiento, nullif(btrim(coalesce(p_tipo_establecimiento_otro, '')), ''),
    p_latitude, p_longitude, p_accuracy_meters, p_location_captured_at,
    coalesce(nullif(p_location_source, ''), 'browser_geolocation'),
    p_context, 'PENDING'
  )
  returning * into v_profile;

  update public.agencies
     set status = 'COMPLETED', updated_at = now()
   where id = p_agency_id;

  insert into public.audit_log (entity_type, entity_id, action, before_data, after_data, actor_email)
  values ('agency_profile', v_profile.id, 'PROFILE_CREATED', null, to_jsonb(v_profile), p_actor);

  return v_profile;
end;
$$;

comment on function public.submit_agency_profile is
  'Crea el perfil y marca la agencia como COMPLETED en una sola transacción.';
