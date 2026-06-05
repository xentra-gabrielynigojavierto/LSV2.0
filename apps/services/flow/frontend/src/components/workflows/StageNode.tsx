"use client";

import type { WorkflowStage } from "@/types/workflow";
import { STATUS_LABELS } from "@/types/task";

export type HandleSide = "left" | "right" | "top" | "bottom";

interface Props {
  stage: WorkflowStage;
  posX: number;
  posY: number;
  isSelected: boolean;
  isDragging: boolean;
  isConnectTarget: boolean;
  connectingFrom: boolean;
  onSelect: (stage: WorkflowStage) => void;
  onMouseDown: (e: React.MouseEvent, stageId: string) => void;
  onHandleMouseDown?: (e: React.MouseEvent, stageId: string, side: HandleSide) => void;
  onHandleMouseUp?: (e: React.MouseEvent, stageId: string, side: HandleSide) => void;
  disabled?: boolean;
}

const HANDLE_BASE = "absolute w-[18px] h-[18px] rounded-full border-2 bg-white z-[5] transition-all";
const OUTPUT_STYLE = "border-blue-400 hover:bg-blue-100 hover:border-blue-500 cursor-crosshair";
const INPUT_STYLE_IDLE = "border-gray-300 hover:border-blue-400 hover:bg-blue-50";
const INPUT_STYLE_ACTIVE = "border-green-500 bg-green-100 scale-125";

export function StageNode({
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
}: Props) {
  const outputVis = connectingFrom ? "opacity-0 pointer-events-none" : "opacity-0 group-hover:opacity-100";
  const inputVis = isConnectTarget ? "opacity-100" : "opacity-0 pointer-events-none";
  const inputStyle = isConnectTarget ? INPUT_STYLE_ACTIVE : INPUT_STYLE_IDLE;

  return (
    <div
      onMouseDown={(e) => onMouseDown(e, stage.id)}
      onMouseUp={(e) => {
        if (isConnectTarget && onHandleMouseUp) {
          onHandleMouseUp(e, stage.id, "left");
        }
      }}
      onClick={(e) => {
        e.stopPropagation();
        onSelect(stage);
      }}
      className={`absolute select-none rounded-lg border-2 bg-white px-4 py-3 shadow-sm transition-shadow w-[180px] group ${
        isDragging ? "shadow-lg z-50 cursor-grabbing" : "cursor-grab hover:shadow-md"
      } ${isSelected ? "border-blue-500 ring-2 ring-blue-200" : isConnectTarget ? "border-green-400 ring-2 ring-green-200" : "border-gray-200"}`}
      style={{
        left: posX,
        top: posY,
        opacity: isDragging ? 0.85 : 1,
      }}
    >
      <div className="flex items-center gap-1.5 mb-1">
        <span className="text-sm font-semibold text-gray-900 truncate">{stage.name}</span>
      </div>
      <div className="flex items-center gap-1.5 flex-wrap">
        <span className="text-[11px] text-gray-400 font-mono">{stage.key}</span>
        {stage.isInitial && (
          <span className="inline-flex items-center rounded-full bg-green-100 px-1.5 py-0.5 text-[10px] font-medium text-green-700">
            Initial
          </span>
        )}
        {stage.isTerminal && (
          <span className="inline-flex items-center rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium text-gray-600">
            Terminal
          </span>
        )}
      </div>
      <p className="text-[11px] text-gray-500 mt-1">→ {STATUS_LABELS[stage.mappedStatus]}</p>

      {!disabled && onHandleMouseDown && (
        <>
          <div
            className={`${HANDLE_BASE} ${OUTPUT_STYLE} top-1/2 -right-[10px] -translate-y-1/2 ${outputVis}`}
            onMouseDown={(e) => { e.stopPropagation(); e.preventDefault(); onHandleMouseDown(e, stage.id, "right"); }}
            title="Drag to connect"
            data-handle-side="right"
            data-stage-id={stage.id}
          />
          <div
            className={`${HANDLE_BASE} ${OUTPUT_STYLE} top-1/2 -left-[10px] -translate-y-1/2 ${outputVis}`}
            onMouseDown={(e) => { e.stopPropagation(); e.preventDefault(); onHandleMouseDown(e, stage.id, "left"); }}
            title="Drag to connect"
            data-handle-side="left"
            data-stage-id={stage.id}
          />
          <div
            className={`${HANDLE_BASE} ${OUTPUT_STYLE} left-1/2 -top-[10px] -translate-x-1/2 ${outputVis}`}
            onMouseDown={(e) => { e.stopPropagation(); e.preventDefault(); onHandleMouseDown(e, stage.id, "top"); }}
            title="Drag to connect"
            data-handle-side="top"
            data-stage-id={stage.id}
          />
          <div
            className={`${HANDLE_BASE} ${OUTPUT_STYLE} left-1/2 -bottom-[10px] -translate-x-1/2 ${outputVis}`}
            onMouseDown={(e) => { e.stopPropagation(); e.preventDefault(); onHandleMouseDown(e, stage.id, "bottom"); }}
            title="Drag to connect"
            data-handle-side="bottom"
            data-stage-id={stage.id}
          />
        </>
      )}

      {!disabled && onHandleMouseUp && (
        <>
          <div
            className={`${HANDLE_BASE} ${inputStyle} top-1/2 -left-[10px] -translate-y-1/2 ${inputVis}`}
            onMouseUp={(e) => { onHandleMouseUp(e, stage.id, "left"); }}
          />
          <div
            className={`${HANDLE_BASE} ${inputStyle} top-1/2 -right-[10px] -translate-y-1/2 ${inputVis}`}
            onMouseUp={(e) => { onHandleMouseUp(e, stage.id, "right"); }}
          />
          <div
            className={`${HANDLE_BASE} ${inputStyle} left-1/2 -top-[10px] -translate-x-1/2 ${inputVis}`}
            onMouseUp={(e) => { onHandleMouseUp(e, stage.id, "top"); }}
          />
          <div
            className={`${HANDLE_BASE} ${inputStyle} left-1/2 -bottom-[10px] -translate-x-1/2 ${inputVis}`}
            onMouseUp={(e) => { onHandleMouseUp(e, stage.id, "bottom"); }}
          />
        </>
      )}
    </div>
  );
}
