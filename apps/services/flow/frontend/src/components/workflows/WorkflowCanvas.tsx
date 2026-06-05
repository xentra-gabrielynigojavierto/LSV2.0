"use client";

import { useState, useRef, useCallback, useEffect, memo, useMemo } from "react";
import type { WorkflowStage, WorkflowTransition } from "@/types/workflow";
import type { CreateStageRequest } from "@/types/workflow";
import { StageNode } from "./StageNode";
import type { HandleSide } from "./StageNode";
import { WorkflowEdges, NODE_WIDTH, NODE_HEIGHT, HANDLE_OFFSET_Y, getPortForSide } from "./WorkflowEdges";
import { computeOrthogonalPoints, pointsToPolyline } from "./ConnectionLine";
import { CanvasContextMenu } from "./CanvasContextMenu";

interface Props {
  stages: WorkflowStage[];
  transitions: WorkflowTransition[];
  onUpdatePosition: (stageId: string, canvasX: number, canvasY: number) => Promise<void>;
  onSelectStage: (stage: WorkflowStage | null) => void;
  selectedStageId: string | null;
  onCreateTransition?: (fromStageId: string, toStageId: string) => Promise<void>;
  onDeleteTransition?: (transitionId: string) => Promise<void>;
  onAddStage?: (stage: CreateStageRequest) => Promise<void>;
  disabled?: boolean;
}

const GRID_SIZE = 20;
const DEFAULT_SPACING_X = 220;
const DEFAULT_SPACING_Y = 100;
const CANVAS_PADDING = 40;

function getDefaultPosition(index: number, total: number): { x: number; y: number } {
  const cols = Math.ceil(Math.sqrt(total));
  const col = index % cols;
  const row = Math.floor(index / cols);
  return {
    x: CANVAS_PADDING + col * DEFAULT_SPACING_X,
    y: CANVAS_PADDING + row * DEFAULT_SPACING_Y,
  };
}

function snapToGrid(value: number): number {
  return Math.round(value / GRID_SIZE) * GRID_SIZE;
}

function getStagePosition(stage: WorkflowStage, index: number, total: number): { x: number; y: number } {
  if (stage.canvasX != null && stage.canvasY != null) {
    return { x: stage.canvasX, y: stage.canvasY };
  }
  return getDefaultPosition(index, total);
}

export function WorkflowCanvas({
  stages,
  transitions,
  onUpdatePosition,
  onSelectStage,
  selectedStageId,
  onCreateTransition,
  onDeleteTransition,
  onAddStage,
  disabled,
}: Props) {
  const [draggingId, setDraggingId] = useState<string | null>(null);
  const [localPositions, setLocalPositions] = useState<Record<string, { x: number; y: number }>>({});
  const [saving, setSaving] = useState(false);
  const [connectingFrom, setConnectingFrom] = useState<string | null>(null);
  const [connectingFromSide, setConnectingFromSide] = useState<HandleSide>("right");
  const [connectPreview, setConnectPreview] = useState<{ x: number; y: number } | null>(null);
  const [connectTarget, setConnectTarget] = useState<string | null>(null);
  const [connectError, setConnectError] = useState<string | null>(null);
  const [contextMenu, setContextMenu] = useState<{ screenX: number; screenY: number; canvasX: number; canvasY: number } | null>(null);
  const canvasRef = useRef<HTMLDivElement>(null);

  const dragStateRef = useRef<{
    stageId: string;
    startX: number;
    startY: number;
    origX: number;
    origY: number;
  } | null>(null);

  const localPosRef = useRef(localPositions);
  localPosRef.current = localPositions;

  const onUpdatePositionRef = useRef(onUpdatePosition);
  onUpdatePositionRef.current = onUpdatePosition;

  const connectingFromRef = useRef(connectingFrom);
  connectingFromRef.current = connectingFrom;

  const connectTargetRef = useRef(connectTarget);
  connectTargetRef.current = connectTarget;

  const onCreateTransitionRef = useRef(onCreateTransition);
  onCreateTransitionRef.current = onCreateTransition;

  const nodePositions = useMemo(() => {
    return stages.map((stage, index) => {
      const pos = localPositions[stage.id] ?? getStagePosition(stage, index, stages.length);
      return { id: stage.id, x: pos.x, y: pos.y };
    });
  }, [stages, localPositions]);

  const contentExtent = useMemo(() => {
    let maxX = 0;
    let maxY = 0;
    for (const np of nodePositions) {
      maxX = Math.max(maxX, np.x + NODE_WIDTH + CANVAS_PADDING);
      maxY = Math.max(maxY, np.y + NODE_HEIGHT + CANVAS_PADDING);
    }
    return { width: Math.max(maxX, 600), height: Math.max(maxY, 400) };
  }, [nodePositions]);

  const handleMouseDown = useCallback((e: React.MouseEvent, stageId: string) => {
    if (disabled || e.button !== 0 || connectingFromRef.current) return;
    e.preventDefault();
    e.stopPropagation();

    const stageIndex = stages.findIndex((s) => s.id === stageId);
    if (stageIndex === -1) return;
    const stage = stages[stageIndex];

    const pos = localPosRef.current[stageId] ?? getStagePosition(stage, stageIndex, stages.length);

    dragStateRef.current = {
      stageId,
      startX: e.clientX,
      startY: e.clientY,
      origX: pos.x,
      origY: pos.y,
    };
    setDraggingId(stageId);
  }, [disabled, stages]);

  const handleOutputHandleMouseDown = useCallback((e: React.MouseEvent, stageId: string, side: HandleSide = "right") => {
    if (disabled) return;
    e.stopPropagation();
    e.preventDefault();
    setConnectingFrom(stageId);
    connectingFromRef.current = stageId;
    setConnectingFromSide(side);
    setConnectError(null);

    const rect = canvasRef.current?.getBoundingClientRect();
    if (rect) {
      const scrollLeft = canvasRef.current?.scrollLeft ?? 0;
      const scrollTop = canvasRef.current?.scrollTop ?? 0;
      setConnectPreview({
        x: e.clientX - rect.left + scrollLeft,
        y: e.clientY - rect.top + scrollTop,
      });
    }
  }, [disabled]);

  const handleInputHandleMouseUp = useCallback((_e: React.MouseEvent, stageId: string) => {
    if (connectingFromRef.current && stageId === connectingFromRef.current) return;
    setConnectTarget(stageId);
    connectTargetRef.current = stageId;
  }, []);

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (connectingFromRef.current) {
        const rect = canvasRef.current?.getBoundingClientRect();
        if (rect) {
          const scrollLeft = canvasRef.current?.scrollLeft ?? 0;
          const scrollTop = canvasRef.current?.scrollTop ?? 0;
          setConnectPreview({
            x: e.clientX - rect.left + scrollLeft,
            y: e.clientY - rect.top + scrollTop,
          });
        }
        return;
      }

      const ds = dragStateRef.current;
      if (!ds) return;
      const dx = e.clientX - ds.startX;
      const dy = e.clientY - ds.startY;
      const newX = snapToGrid(Math.max(0, ds.origX + dx));
      const newY = snapToGrid(Math.max(0, ds.origY + dy));

      setLocalPositions((prev) => ({
        ...prev,
        [ds.stageId]: { x: newX, y: newY },
      }));
    };

    const handleMouseUp = async () => {
      if (connectingFromRef.current) {
        const fromId = connectingFromRef.current;
        const toId = connectTargetRef.current;
        connectingFromRef.current = null;
        connectTargetRef.current = null;
        setConnectingFrom(null);
        setConnectPreview(null);
        setConnectTarget(null);

        if (toId && fromId !== toId && onCreateTransitionRef.current) {
          try {
            await onCreateTransitionRef.current(fromId, toId);
          } catch (err) {
            setConnectError(err instanceof Error ? err.message : "Failed to create transition");
            setTimeout(() => setConnectError(null), 3000);
          }
        }
        return;
      }

      const ds = dragStateRef.current;
      if (!ds) return;
      dragStateRef.current = null;

      const pos = localPosRef.current[ds.stageId];
      setDraggingId(null);

      if (pos) {
        setSaving(true);
        try {
          await onUpdatePositionRef.current(ds.stageId, pos.x, pos.y);
          setLocalPositions((prev) => {
            const next = { ...prev };
            delete next[ds.stageId];
            return next;
          });
        } catch {
          setLocalPositions((prev) => {
            const next = { ...prev };
            delete next[ds.stageId];
            return next;
          });
        } finally {
          setSaving(false);
        }
      }
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);

    return () => {
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
    };
  }, []);

  const handleCanvasClick = useCallback(() => {
    onSelectStage(null);
    setContextMenu(null);
  }, [onSelectStage]);

  const handleContextMenu = useCallback((e: React.MouseEvent) => {
    if (disabled || !onAddStage) return;
    e.preventDefault();
    e.stopPropagation();
    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;
    const scrollLeft = canvasRef.current?.scrollLeft ?? 0;
    const scrollTop = canvasRef.current?.scrollTop ?? 0;
    const cx = snapToGrid(e.clientX - rect.left + scrollLeft);
    const cy = snapToGrid(e.clientY - rect.top + scrollTop);
    setContextMenu({ screenX: e.clientX, screenY: e.clientY, canvasX: cx, canvasY: cy });
  }, [disabled, onAddStage]);

  const handleNodeSelect = useCallback((stage: WorkflowStage) => {
    onSelectStage(stage);
  }, [onSelectStage]);

  const handleDeleteTransition = useCallback(async (transitionId: string) => {
    if (!onDeleteTransition) return;
    try {
      await onDeleteTransition(transitionId);
    } catch (err) {
      setConnectError(err instanceof Error ? err.message : "Failed to delete transition");
      setTimeout(() => setConnectError(null), 3000);
    }
  }, [onDeleteTransition]);

  const connectPreviewLine = useMemo(() => {
    if (!connectingFrom || !connectPreview) return null;
    const fromNode = nodePositions.find((n) => n.id === connectingFrom);
    if (!fromNode) return null;
    const port = getPortForSide(fromNode, connectingFromSide);
    const direction = (connectingFromSide === "top" || connectingFromSide === "bottom") ? "vertical" as const : "horizontal" as const;
    return {
      fromX: port.x,
      fromY: port.y,
      toX: connectPreview.x,
      toY: connectPreview.y,
      direction,
    };
  }, [connectingFrom, connectingFromSide, connectPreview, nodePositions]);

  return (
    <div className="relative">
      {disabled && (
        <div className="absolute inset-0 z-40 flex items-center justify-center bg-white/60 rounded-lg">
          <p className="text-sm text-gray-500 bg-white px-4 py-2 rounded-lg shadow-sm border border-gray-200">
            Canvas editing requires backend connection
          </p>
        </div>
      )}
      <div
        ref={canvasRef}
        onClick={handleCanvasClick}
        onContextMenu={handleContextMenu}
        className="relative w-full overflow-auto rounded-lg border border-gray-200 bg-gray-50"
        style={{
          maxHeight: 500,
          backgroundImage:
            "radial-gradient(circle, #d1d5db 1px, transparent 1px)",
          backgroundSize: `${GRID_SIZE}px ${GRID_SIZE}px`,
        }}
      >
        <div
          style={{
            position: "relative",
            width: contentExtent.width,
            minWidth: "100%",
            height: contentExtent.height,
            minHeight: 400,
          }}
        >
          <svg
            width={contentExtent.width}
            height={contentExtent.height}
            style={{ position: "absolute", top: 0, left: 0, zIndex: 1 }}
          >
            <WorkflowEdges
              transitions={transitions}
              nodePositions={nodePositions}
              onDelete={onDeleteTransition ? handleDeleteTransition : undefined}
              disabled={disabled}
            />
            {connectPreviewLine && (
              <polyline
                points={pointsToPolyline(
                  computeOrthogonalPoints(
                    connectPreviewLine.fromX,
                    connectPreviewLine.fromY,
                    connectPreviewLine.toX,
                    connectPreviewLine.toY,
                    connectPreviewLine.direction,
                  ),
                )}
                fill="none"
                stroke="#3b82f6"
                strokeWidth={2}
                strokeDasharray="6 4"
                strokeLinejoin="round"
                style={{ pointerEvents: "none" }}
              />
            )}
          </svg>

          <div style={{ position: "relative", zIndex: 2 }}>
            {stages.map((stage, index) => {
              const pos = localPositions[stage.id] ?? getStagePosition(stage, index, stages.length);
              return (
                <MemoStageNode
                  key={stage.id}
                  stage={stage}
                  posX={pos.x}
                  posY={pos.y}
                  isSelected={selectedStageId === stage.id}
                  isDragging={draggingId === stage.id}
                  isConnectTarget={connectingFrom != null && connectingFrom !== stage.id}
                  connectingFrom={connectingFrom === stage.id}
                  onSelect={handleNodeSelect}
                  onMouseDown={handleMouseDown}
                  onHandleMouseDown={handleOutputHandleMouseDown}
                  onHandleMouseUp={handleInputHandleMouseUp}
                  disabled={disabled}
                />
              );
            })}
          </div>

          {stages.length === 0 && (
            <div className="absolute inset-0 flex items-center justify-center" style={{ zIndex: 3 }}>
              <p className="text-sm text-gray-400">No stages yet. Right-click the canvas to add a stage.</p>
            </div>
          )}
        </div>
      </div>
      {saving && (
        <div className="absolute top-2 right-2 flex items-center gap-1.5 bg-white/90 rounded px-2 py-1 shadow-sm border border-gray-200 z-50">
          <div className="h-3 w-3 animate-spin rounded-full border-2 border-gray-300 border-t-blue-600" />
          <span className="text-xs text-gray-500">Saving…</span>
        </div>
      )}
      {connectError && (
        <div className="absolute bottom-2 left-1/2 -translate-x-1/2 bg-red-50 border border-red-200 rounded-lg px-3 py-1.5 shadow-sm z-50">
          <p className="text-xs text-red-700">{connectError}</p>
        </div>
      )}
      {connectingFrom && (
        <div className="absolute top-2 left-2 bg-blue-50 border border-blue-200 rounded-lg px-3 py-1.5 shadow-sm z-50">
          <p className="text-xs text-blue-700">Drop on a stage to create a transition</p>
        </div>
      )}
      {!connectingFrom && !contextMenu && onAddStage && stages.length > 0 && (
        <div className="absolute bottom-2 left-2 z-50 pointer-events-none">
          <p className="text-[10px] text-gray-400">Right-click to add a stage</p>
        </div>
      )}
      {contextMenu && onAddStage && (
        <CanvasContextMenu
          x={contextMenu.screenX}
          y={contextMenu.screenY}
          canvasX={contextMenu.canvasX}
          canvasY={contextMenu.canvasY}
          onAddStage={onAddStage}
          onClose={() => setContextMenu(null)}
          nextOrder={stages.length}
          hasInitial={stages.some((s) => s.isInitial)}
        />
      )}
    </div>
  );
}

interface MemoNodeProps {
  stage: WorkflowStage;
  posX: number;
  posY: number;
  isSelected: boolean;
  isDragging: boolean;
  isConnectTarget: boolean;
  connectingFrom: boolean;
  onSelect: (stage: WorkflowStage) => void;
  onMouseDown: (e: React.MouseEvent, stageId: string) => void;
  onHandleMouseDown: (e: React.MouseEvent, stageId: string, side: HandleSide) => void;
  onHandleMouseUp: (e: React.MouseEvent, stageId: string, side: HandleSide) => void;
  disabled?: boolean;
}

const MemoStageNode = memo(function MemoStageNode({
  stage,
  posX,
  posY,
  isSelected,
  isDragging,
  isConnectTarget,
  connectingFrom,
  onSelect,
  onMouseDown,
  onHandleMouseDown,
  onHandleMouseUp,
  disabled,
}: MemoNodeProps) {
  return (
    <StageNode
      stage={stage}
      posX={posX}
      posY={posY}
      isSelected={isSelected}
      isDragging={isDragging}
      isConnectTarget={isConnectTarget}
      connectingFrom={connectingFrom}
      onSelect={onSelect}
      onMouseDown={onMouseDown}
      onHandleMouseDown={onHandleMouseDown}
      onHandleMouseUp={onHandleMouseUp}
      disabled={disabled}
    />
  );
});
