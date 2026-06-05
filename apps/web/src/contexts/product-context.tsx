'use client';

import { createContext, useContext, useState, useEffect, useMemo } from 'react';
import { usePathname } from 'next/navigation';
import { inferProductFromPath } from '@/lib/nav';

interface ProductContextValue {
  selectedProductId: string | null;
  setSelectedProductId: (id: string | null) => void;
}

const ProductContext = createContext<ProductContextValue>({
  selectedProductId: null,
  setSelectedProductId: () => {},
});

export function ProductProvider({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  const [selectedProductId, setSelectedProductId] = useState<string | null>(
    () => inferProductFromPath(pathname ?? ''),
  );

  useEffect(() => {
    const inferred = inferProductFromPath(pathname ?? '');
    if (inferred) setSelectedProductId(inferred);
  }, [pathname]);

  const value = useMemo(
    () => ({ selectedProductId, setSelectedProductId }),
    [selectedProductId],
  );

  return (
    <ProductContext.Provider value={value}>
      {children}
    </ProductContext.Provider>
  );
}

export function useProduct(): ProductContextValue {
  return useContext(ProductContext);
}
