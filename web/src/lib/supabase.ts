import { createClient, type SupabaseClient } from "@supabase/supabase-js";

// Supabase remove os tokens da URL ao iniciar; capture o tipo do link antes disso.
export const initialAuthRedirectType = typeof window === "undefined"
  ? null
  : new URLSearchParams(window.location.hash.slice(1)).get("type");

const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
const key = process.env.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY;

export const isDemo = process.env.NODE_ENV === "development" && (!url || !key);

let client: SupabaseClient | null = null;

export function getSupabase() {
  if (!url || !key) return null;
  client ??= createClient(url, key, {
    auth: { persistSession: true, autoRefreshToken: true, detectSessionInUrl: true },
  });
  return client;
}
