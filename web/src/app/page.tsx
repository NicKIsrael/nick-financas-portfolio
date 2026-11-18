import { Suspense } from "react";
import FinanceApp from "@/components/FinanceApp";

export default function Home() {
  return <Suspense fallback={<div className="splash">Preparando seu financeiro...</div>}><FinanceApp /></Suspense>;
}
