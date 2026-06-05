"use client";

import { memo, useState, useMemo } from "react";
import type { PortDirection } from "./WorkflowEdges";

interface Props {
  id: string;
  fromX: number;
  fromY: number;
  toX: number;
  toY: number;
  direction?: PortDirection;
  label?: string;
  isActive?: boolean;
  onDelete?: (id: string) => void;
  disabled?: boolean;
}

const ARROWHEAD_SIZE = 8;
const MIN_STUB = 20;

function computeOrthogonalPoints(
  fromX: number,
  fromY: number,
  toX: number,
  toY: number,
  direction: PortDirection = "horizontal",
): Array<[number, number]> {
  if (direction === "vertical") {
    const dy = toY - fromY;
    if (Math.abs(dy) < MIN_STUB * 2) {
      const stubY = fromY + (dy >= 0 ? MIN_STUB : -MIN_STUB);
      return [
        [fromX, fromY],
        [fromX, stubY],
        [toX, stubY],
        [toX, toY],
      ];
    }
    const midY = (fromY + toY) / 2;
    return [
      [fromX, fromY],
      [fromX, midY],
      [toX, midY],
      [toX, toY],
    ];
  }

  const dx = toX - fromX;
  if (Math.abs(dx) < MIN_STUB * 2) {
    const stubX = fromX + (dx >= 0 ? MIN_STUB : -MIN_STUB);
    return [
      [fromX, fromY],
      [stubX, fromY],
      [stubX, toY],
      [toX, toY],
    ];
  }

  const midX = (fromX + toX) / 2;
  return [
    [fromX, fromY],
    [midX, fromY],
    [midX, toY],
    [toX, toY],
  ];
}

function pointsToPolyline(pts: Array<[number, number]>): string {
  return pts.map(([x, y]) => `${x},${y}`).join(" ");
}

function ConnectionLineInner({ id, fromX, fromY, toX, toY, direction = "horizontal", label, isActive = true, onDelete, disabled }: Props) {
  const [hovered, setHovered] = useState(false);

  const points = useMemo(
    () => computeOrthogonalPoints(fromX, fromY, toX, toY, direction),
    [fromX, fromY, toX, toY, direction],
  );

  const polyStr = useMemo(() => pointsToPolyline(points), [points]);

  const last = points[points.length - 1];
  const prev = points[points.length - 2];
  const arrowAngle = Math.atan2(last[1] - prev[1], last[0] - prev[0]);
  const arrowX = last[0] - Math.cos(arrowAngle) * 2;
  const arrowY = last[1] - Math.sin(arrowAngle) * 2;
  const a1x = arrowX - ARROWHEAD_SIZE * Math.cos(arrowAngle - Math.PI / 6);
  const a1y = arrowY - ARROWHEAD_SIZE * Math.sin(arrowAngle - Math.PI / 6);
  const a2x = arrowX - ARROWHEAD_SIZE * Math.cos(arrowAngle + Math.PI / 6);
  const a2y = arrowY - ARROWHEAD_SIZE * Math.sin(arrowAngle + Math.PI / 6);

  const midIdx = Math.floor(points.length / 2);
  const labelX = (points[midIdx - 1][0] + points[midIdx][0]) / 2;
  const labelY = (points[midIdx - 1][1] + points[midIdx][1]) / 2;

  const strokeColor = !isActive ? "#d1d5db" : hovered ? "#3b82f6" : "#94a3b8";
  const strokeWidth = hovered ? 2.5 : 1.5;

  return (
    <g
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{ pointerEvents: "auto" }}
    >
      <polyline
        points={polyStr}
        fill="none"
        stroke="transparent"
        strokeWidth={12}
        strokeLinejoin="round"
        style={{ cursor: onDelete && !disabled ? "pointer" : "default" }}
      />
      <polyline
        points={polyStr}
        fill="none"
        stroke={strokeColor}
        strokeWidth={strokeWidth}
        strokeLinejoin="round"
        strokeDasharray={!isActive ? "4 4" : undefined}
        style={{ pointerEvents: "none", transition: "stroke 0.15s, stroke-width 0.15s" }}
      />
      <polygon
        points={`${arrowX},${arrowY} ${a1x},${a1y} ${a2x},${a2y}`}
        fill={strokeColor}
        style={{ pointerEvents: "none", transition: "fill 0.15s" }}
      />
      {hovered && label && (
        <g>
          <rect
            x={labelX - 40}
            y={labelY - 10}
            width={80}
            height={20}
            rx={4}
            fill="white"
            stroke="#e5e7eb"
            strokeWidth={1}
            style={{ pointerEvents: "none" }}
          />
          <text
            x={labelX}
            y={labelY + 4}
            textAnchor="middle"
            fill="#6b7280"
            fontSize={10}
            fontFamily="system-ui, sans-serif"
            style={{ pointerEvents: "none" }}
          >
            {label.length > 14 ? label.slice(0, 12) + "…" : label}
          </text>
        </g>
      )}
      {hovered && onDelete && !disabled && (
        <g
          onClick={(e) => {
            e.stopPropagation();
            onDelete(id);
          }}
          style={{ cursor: "pointer" }}
        >
          <circle
            cx={labelX}
            cy={labelY - (label ? 18 : 0)}
            r={10}
            fill="white"
            stroke="#ef4444"
            strokeWidth={1.5}
          />
          <line
            x1={labelX - 3.5}
            y1={(labelY - (label ? 18 : 0)) - 3.5}
            x2={labelX + 3.5}
            y2={(labelY - (label ? 18 : 0)) + 3.5}
            stroke="#ef4444"
            strokeWidth={2}
            strokeLinecap="round"
          />
          <line
            x1={labelX + 3.5}
            y1={(labelY - (label ? 18 : 0)) - 3.5}
            x2={labelX - 3.5}
            y2={(labelY - (label ? 18 : 0)) + 3.5}
            stroke="#ef4444"
            strokeWidth={2}
            strokeLinecap="round"
          />
        </g>
      )}
    </g>
  );
}

export const ConnectionLine = memo(ConnectionLineInner);

export { computeOrthogonalPoints, pointsToPolyline };
