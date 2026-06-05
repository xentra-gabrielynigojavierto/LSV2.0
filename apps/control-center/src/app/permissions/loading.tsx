export default function PermissionsLoading() {
  return (
    <div className="space-y-4 animate-pulse">
      <div className="h-7 w-52 bg-gray-200 rounded" />
      <div className="h-4 w-64 bg-gray-100 rounded" />
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="h-10 bg-gray-50 border-b border-gray-100" />
        {Array.from({ length: 10 }).map((_, i) => (
          <div key={i} className="h-12 border-b border-gray-100 flex items-center px-4 gap-4">
            <div className="h-4 w-40 bg-gray-100 rounded" />
            <div className="h-4 w-52 bg-gray-100 rounded" />
            <div className="h-4 w-20 bg-gray-100 rounded" />
          </div>
        ))}
      </div>
    </div>
  );
}
