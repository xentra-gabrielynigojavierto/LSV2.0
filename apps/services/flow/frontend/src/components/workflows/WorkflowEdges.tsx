"use client";

import { memo, useMemo } from "react";
import type { WorkflowTransition } from "@/types/workflow";
import { ConnectionLine } from "./ConnectionLine";
import type { HandleSide } from "./StageNode";

export const NODE_WIDTH = 180;
export const NODE_HEIGHT = 72;
export const HANDLE_OFFSET_Y = NODE_HEIGHT / 2;

interface NodePosition {
  id: string;
  x: number;
  y: number;
}

interface Props {
  transitions: WorkflowTransition[];
  nodePositions: NodePosition[];
  onDelete?: (transitionId: string) => void;
  disabled?: boolean;
}

export type PortDirection = "horizontal" | "vertical";

interface PortResult {
  fromX: number;
  fromY: number;
  toX: number;
  toY: number;
  direction: PortDirection;
}

function getPortPosition(pos: NodePosition, side: HandleSide): { x: number; y: number } {
  switch (side) {
    case "right":  return { x: pos.x + NODE_WIDTH, y: pos.y + HANDLE_OFFSET_Y };
    case "left":   return { x: pos.x, y: pos.y + HANDLE_OFFSET_Y };
    case "top":    return { x: pos.x + NODE_WIDTH / 2, y: pos.y };
    case "bottom": return { x: pos.x + NODE_WIDTH / 2, y: pos.y + NODE_HEIGHT };
  }
}

export function getBestPorts(from: NodePosition, to: NodePosition): PortResult {
  const fcx = from.x + NODE_WIDTH / 2;
  const fcy = from.y + NODE_HEIGHT / 2;
  const tcx = to.x + NODE_WIDTH / 2;
  const tcy = to.y + NODE_HEIGHT / 2;
  const dx = tcx - fcx;
  const dy = tcy - fcy;

  let fromSide: HandleSide;
  let toSide: HandleSide;

  if (Math.abs(dx) >= Math.abs(dy)) {
    fromSide = dx >= 0 ? "right" : "left";
    toSide = dx >= 0 ? "left" : "right";
  } else {
    fromSide = dy >= 0 ? "bottom" : "top";
    toSide = dy >= 0 ? "top" : "bottom";
  }

  const fromPt = getPortPosition(from, fromSide);
  const toPt = getPortPosition(to, toSide);
  const direction: PortDirection = (fromSide === "top" || fromSide === "bottom") ? "vertical" : "horizontal";

  return { fromX: fromPt.x, fromY: fromPt.y, toX: toPt.x, toY: toPt.y, direction };
}

export function getPortForSide(pos: NodePosition, side: HandleSide): { x: number; y: number } {
  return getPortPosition(pos, side);
}

function WorkflowEdgesInner({ transitions, nodePositions, onDelete, disabled }: Props) {
  const posMap = useMemo(() => {
    const m = new Map<string, NodePosition>();
    for (const np of nodePositions) m.set(np.id, np);
    return m;
  }, [nodePositions]);

  const edges = useMemo(() => {
    return transitions
      .map((t) => {
        const from = posMap.get(t.fromStageId);
        const to = posMap.get(t.toStageId);
        if (!from || !to) return null;
        const ports = getBestPorts(from, to);
        return {
          id: t.id,
          ...ports,
          label: t.name,
          isActive: t.isActive,
        };
      })
      .filter(Boolean) as Array<{
        id: string;
        fromX: number;
        fromY: number;
        toX: number;
        toY: number;
        direction: PortDirection;
        label: string;
        isActive: boolean;
      }>;
  }, [transitions, posMap]);

  if (edges.length === 0) return null;

  return (
    <>
      {edges.map((edge) => (
        <ConnectionLine
          key={edge.id}
          id={edge.id}
          fromX={edge.fromX}
          fromY={edge.fromY}
          toX={edge.toX}
          toY={edge.toY}
          direction={edge.direction}
          label={edge.label}
          isActive={edge.isActive}
          onDelete={onDelete}
          disabled={disabled}
        />
      ))}
    </>
  );
}

export const WorkflowEdges = memo(WorkflowEdgesInner);
