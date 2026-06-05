'use client';

import { orgTypeLabel } from '@/lib/nav';
import type { OrgTypeValue } from '@/types';

interface OrgBadgeProps {
  orgType?: OrgTypeValue;
  orgName?: string;
}

export function OrgBadge({ orgType, orgName }: OrgBadgeProps) {
  return (
    <div className="flex flex-col leading-tight">
      <span className="text-xs font-medium text-gray-400 uppercase tracking-wider">
        {orgTypeLabel(orgType)}
      </span>
      {orgName && (
        <span className="text-sm font-semibold text-gray-900 truncate max-w-[160px]">
          {orgName}
        </span>
      )}
    </div>
  );
}
