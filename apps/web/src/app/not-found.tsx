import Link from 'next/link';

export default function NotFound() {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center text-center p-8 bg-gray-50">
      <h1 className="text-5xl font-bold text-gray-900 mb-2">404</h1>
      <p className="text-gray-500 mb-6">Page not found.</p>
      <Link
        href="/"
        className="px-5 py-2 rounded-md bg-gray-900 text-white text-sm hover:bg-gray-700 transition-colors"
      >
        Go home
      </Link>
    </div>
  );
}
