// Allow CSS file imports in TypeScript (for Next.js global stylesheets)
declare module '*.css' {
  const content: Record<string, string>;
  export default content;
}
