import "./globals.css";

export const metadata = {
  title: "LoL Live Coach | Le coach live premium pour League of Legends",
  description:
    "Lis le bon timing, garde ton focus et joue avec plus de lucidite. LoL Live Coach t'accompagne en direct avec un overlay propre, des conseils contextuels et un achat simple."
};

export default function RootLayout({ children }) {
  return (
    <html lang="fr">
      <body>{children}</body>
    </html>
  );
}
