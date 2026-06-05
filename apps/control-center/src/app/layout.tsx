import type { Metadata } from 'next';
import 'remixicon/fonts/remixicon.css';
import './globals.css';
import { ClientProviders } from '@/providers/client-providers';

export const metadata: Metadata = {
  title: 'LegalSynq Control Center',
  description: 'Platform administration for LegalSynq',
  icons: {
    icon: '/favicon.png',
  },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="antialiased bg-gray-50">
        <ClientProviders>
          {children}
        </ClientProviders>
      </body>
    </html>
  );
}
