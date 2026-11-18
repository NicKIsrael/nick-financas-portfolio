import type { Metadata, Viewport } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Nick FinanÃ§as",
  description: "Nosso financeiro, em um só lugar.",
  applicationName: "Nick FinanÃ§as",
  manifest: "/manifest.webmanifest",
  icons: { icon: "/icon.png", apple: "/icon.png" },
  appleWebApp: { capable: true, statusBarStyle: "default", title: "Financeiro" },
};

export const viewport: Viewport = { width: "device-width", initialScale: 1, themeColor: "#132d27" };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="pt-BR"><body>{children}</body></html>;
}
