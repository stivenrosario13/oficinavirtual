import type { Metadata, Viewport } from "next";
import "./globals.css";

// Next.js requires route metadata to be exported from the layout module.
// eslint-disable-next-line react-refresh/only-export-components
export const metadata: Metadata = {
  title: "Registro de Agencias",
  description:
    "Registro operativo y geográfico de agencias. Completa la información de tu agencia.",
  robots: { index: false, follow: false },
};

// Next.js requires viewport configuration to be exported from this module.
// eslint-disable-next-line react-refresh/only-export-components
export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  themeColor: "#2563eb",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="es" className="h-full antialiased">
      <body className="min-h-full">{children}</body>
    </html>
  );
}
