import { getSupabase } from "./supabase";

export const exportTables = [
  "accounts", "categories", "transactions", "credit_cards", "card_charges", "card_payments",
  "payables", "payable_payments", "recurring_payables", "reserves", "reserve_movements", "goals",
] as const;

export type FinanceExport = {
  format: "nickfinancas-export";
  version: 1;
  exported_at: string;
  tables: Record<(typeof exportTables)[number], Record<string, unknown>[]>;
};

export async function collectFinanceExport(): Promise<FinanceExport> {
  const client = getSupabase();
  if (!client) throw new Error("A exportação precisa de uma sessão conectada.");
  const { data: auth, error: authError } = await client.auth.getUser();
  if (authError || !auth.user) throw new Error("Entre novamente antes de exportar.");
  const tables = {} as FinanceExport["tables"];
  for (const table of exportTables) {
    const rows: Record<string, unknown>[] = [];
    for (let offset = 0; ; offset += 500) {
      const { data, error } = await client.from(table).select("*").order("id").range(offset, offset + 499);
      if (error) throw new Error(`Falha ao exportar ${table}: ${error.message}`);
      rows.push(...(data ?? []));
      if (!data || data.length < 500) break;
    }
    tables[table] = rows;
  }
  return { format: "nickfinancas-export", version: 1, exported_at: new Date().toISOString(), tables };
}

export function downloadFinanceExport(exported: FinanceExport) {
  const date = exported.exported_at.slice(0, 10);
  const blob = new Blob([JSON.stringify(exported, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `nickfinancas-${date}.json`;
  document.body.append(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
