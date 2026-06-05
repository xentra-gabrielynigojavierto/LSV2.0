"use client";

import { Suspense, useCallback, useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { listTasks, createTask, updateTaskStatus } from "@/lib/api/tasks";
import { TaskFilterBar } from "@/components/tasks/TaskFilterBar";
import { TaskTable } from "@/components/tasks/TaskTable";
import { StatusChangeDialog } from "@/components/tasks/StatusChangeDialog";
import { TaskDetailDrawer } from "@/components/tasks/TaskDetailDrawer";
import { CreateTaskDrawer, type CreateTaskFormData } from "@/components/tasks/CreateTaskDrawer";
import { BoardView } from "@/components/tasks/board/BoardView";
import { Pagination } from "@/components/ui/Pagination";
import { ViewToggle } from "@/components/ui/ViewToggle";
import { ErrorBoundary } from "@/components/ui/ErrorBoundary";
import { TenantSwitcher } from "@/components/ui/TenantSwitcher";
import { NavLinks } from "@/components/ui/NavLinks";
import { ProductFilter } from "@/components/ui/ProductFilter";
import {
  DEFAULT_PRODUCT_KEY,
  isValidProductKey,
  normalizeProductKey,
  type ProductKey,
} from "@/lib/productKeys";
import { refreshUnreadCount } from "@/lib/useUnreadCount";
import { addActivityEvent } from "@/lib/activityStore";
import { getActivityMessage } from "@/lib/transitionLabels";
import {
  STATUS_LABELS,
  type TaskResponse,
  type PagedResponse,
  type TaskItemStatus,
} from "@/types/task";

type ViewMode = "list" | "board";

interface Filters {
  status?: TaskItemStatus;
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
  contextType?: string;
  contextId?: string;
  productKey?: ProductKey;
}

function generateLocalId(): string {
  return `local-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

function TaskListPageInner() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [viewMode, setViewMode] = useState<ViewMode>("list");
  const [data, setData] = useState<PagedResponse<TaskResponse> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [localMode, setLocalMode] = useState(false);
  const localTasksRef = useRef<TaskResponse[]>([]);
  const [filters, setFilters] = useState<Filters>(() => {
    const pk = searchParams.get("productKey");
    return isValidProductKey(pk) ? { productKey: pk } : {};
  });
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [sort, setSort] = useState<{ sortBy: string; sortDirection: "asc" | "desc" }>({
    sortBy: "createdAt",
    sortDirection: "desc",
  });
  const [statusDialogTask, setStatusDialogTask] = useState<TaskResponse | null>(null);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [createDrawerOpen, setCreateDrawerOpen] = useState(false);

  const buildLocalData = useCallback((): PagedResponse<TaskResponse> => {
    let items = [...localTasksRef.current];
    if (filters.status) items = items.filter((t) => t.status === filters.status);
    if (filters.assignedToUserId) items = items.filter((t) => t.assignedToUserId === filters.assignedToUserId);
    if (filters.assignedToRoleKey) items = items.filter((t) => t.assignedToRoleKey === filters.assignedToRoleKey);
    if (filters.assignedToOrgId) items = items.filter((t) => t.assignedToOrgId === filters.assignedToOrgId);
    if (filters.contextType) items = items.filter((t) => t.context?.contextType === filters.contextType);
    if (filters.contextId) items = items.filter((t) => t.context?.contextId === filters.contextId);
    if (filters.productKey) items = items.filter((t) => normalizeProductKey(t.productKey) === filters.productKey);

    return {
      items,
      totalCount: items.length,
      page: 1,
      pageSize: items.length || 25,
    };
  }, [filters]);

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listTasks({
        ...filters,
        page: viewMode === "board" ? 1 : page,
        pageSize: viewMode === "board" ? 100 : pageSize,
        sortBy: sort.sortBy,
        sortDirection: sort.sortDirection,
      });
      setLocalMode(false);
      const localItems = localTasksRef.current;
      if (localItems.length > 0) {
        setData({
          ...result,
          items: [...localItems, ...result.items],
          totalCount: result.totalCount + localItems.length,
        });
      } else {
        setData(result);
      }
    } catch (err) {
      console.error("API fetch failed:", err);
      setLocalMode(true);
      setError(null);
      setData(buildLocalData());
    } finally {
      setLoading(false);
    }
  }, [filters, page, pageSize, sort, viewMode, buildLocalData]);

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  useEffect(() => {
    const taskIdParam = searchParams.get("taskId");
    if (taskIdParam) {
      setSelectedTaskId(taskIdParam);
    }
    // Refresh unread count whenever we land on this page (e.g. from a notification click)
    refreshUnreadCount();
  }, [searchParams]);

  const handleDrawerClose = useCallback(() => {
    setSelectedTaskId(null);
    if (searchParams.get("taskId")) {
      const params = new URLSearchParams(searchParams.toString());
      params.delete("taskId");
      const qs = params.toString();
      router.replace(qs ? `/tasks?${qs}` : "/tasks");
    }
  }, [router, searchParams]);

  const handleFilterChange = (newFilters: Filters) => {
    setFilters((prev) => ({ ...newFilters, productKey: prev.productKey }));
    setPage(1);
  };

  useEffect(() => {
    const fromUrl = searchParams.get("productKey");
    const next = isValidProductKey(fromUrl) ? fromUrl : undefined;
    setFilters((prev) => (prev.productKey === next ? prev : { ...prev, productKey: next }));
  }, [searchParams]);

  const handleProductFilterChange = (next: ProductKey | "") => {
    setFilters((prev) => ({ ...prev, productKey: next || undefined }));
    setPage(1);
    const params = new URLSearchParams(searchParams.toString());
    if (next) params.set("productKey", next);
    else params.delete("productKey");
    const qs = params.toString();
    router.replace(qs ? `/tasks?${qs}` : "/tasks");
  };

  const handleViewChange = (mode: ViewMode) => {
    setViewMode(mode);
    setPage(1);
  };

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
  };

  const handlePageSizeChange = (newSize: number) => {
    setPageSize(newSize);
    setPage(1);
  };

  const handleSortChange = (newSort: { sortBy: string; sortDirection: "asc" | "desc" }) => {
    setSort(newSort);
    setPage(1);
  };

  const handleRowClick = (task: TaskResponse) => {
    setSelectedTaskId(task.id);
  };

  const updateLocalTask = useCallback((taskId: string, newStatus: TaskItemStatus) => {
    const idx = localTasksRef.current.findIndex((t) => t.id === taskId);
    if (idx >= 0) {
      localTasksRef.current[idx] = { ...localTasksRef.current[idx], status: newStatus, updatedAt: new Date().toISOString() };
    }
    setData((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        items: prev.items.map((t) =>
          t.id === taskId ? { ...t, status: newStatus, updatedAt: new Date().toISOString() } : t
        ),
      };
    });
  }, []);

  const handleMoveTask = useCallback(async (taskId: string, newStatus: TaskItemStatus) => {
    const existingTask = data?.items.find((t) => t.id === taskId);
    const oldStatus = existingTask?.status;
    if (taskId.startsWith("local-") || localMode) {
      updateLocalTask(taskId, newStatus);
    } else {
      await updateTaskStatus(taskId, { status: newStatus });
      await fetchTasks();
    }
    if (oldStatus && oldStatus !== newStatus) {
      const msg = getActivityMessage(oldStatus, newStatus);
      addActivityEvent(taskId, "STATUS_CHANGED", msg, { from: oldStatus, to: newStatus });
    }
  }, [localMode, fetchTasks, updateLocalTask, data]);

  const handleStatusChange = async (taskId: string, newStatus: TaskItemStatus) => {
    try {
      await handleMoveTask(taskId, newStatus);
      setStatusDialogTask(null);
    } catch (err) {
      alert(err instanceof Error ? err.message : "Failed to update status");
    }
  };

  const handleCreateTask = async (formData: CreateTaskFormData) => {
    const context = formData.contextType && formData.contextId
      ? { contextType: formData.contextType, contextId: formData.contextId }
      : undefined;

    if (!localMode) {
      try {
        const created = await createTask({
          title: formData.title,
          description: formData.description || undefined,
          status: formData.status,
          flowDefinitionId: formData.flowDefinitionId || undefined,
          assignedToUserId: formData.assignedToUserId || undefined,
          assignedToRoleKey: formData.assignedToRoleKey || undefined,
          assignedToOrgId: formData.assignedToOrgId || undefined,
          dueDate: formData.dueDate || undefined,
          context,
          productKey: formData.productKey,
        });
        addActivityEvent(created.id, "CREATED", `Task created: ${formData.title}`);
        await fetchTasks();
        return;
      } catch (err) {
        console.error("Create task API failed, falling back to local:", err);
        setLocalMode(true);
      }
    }

    const now = new Date().toISOString();
    const localTask: TaskResponse = {
      id: generateLocalId(),
      title: formData.title,
      description: formData.description || undefined,
      status: formData.status || "Open",
      assignedToUserId: formData.assignedToUserId || undefined,
      assignedToRoleKey: formData.assignedToRoleKey || undefined,
      assignedToOrgId: formData.assignedToOrgId || undefined,
      dueDate: formData.dueDate || undefined,
      context,
      productKey: formData.productKey || filters.productKey || DEFAULT_PRODUCT_KEY,
      createdAt: now,
      updatedAt: now,
      createdBy: "local",
    };
    localTasksRef.current = [localTask, ...localTasksRef.current];
    addActivityEvent(localTask.id, "CREATED", `Task created: ${formData.title}`);
    setData(buildLocalData());
  };

  const handleDrawerUpdated = (updatedTask?: TaskResponse) => {
    if (updatedTask && updatedTask.id.startsWith("local-")) {
      const idx = localTasksRef.current.findIndex((t) => t.id === updatedTask.id);
      if (idx !== -1) {
        localTasksRef.current[idx] = updatedTask;
      }
      if (localMode) {
        setData(buildLocalData());
        return;
      }
    }
    fetchTasks();
  };

  const totalCount = data?.totalCount ?? 0;
  const tasks = data?.items ?? [];
  const showEmptyState = !loading && tasks.length === 0;
  const showList = tasks.length > 0 && viewMode === "list";
  const showBoard = viewMode === "board";

  return (
    <ErrorBoundary>
    <div className="min-h-screen bg-gray-50">
      <header className="border-b border-gray-200 bg-white">
        <div className={`mx-auto px-4 sm:px-6 lg:px-8 ${viewMode === "list" ? "max-w-7xl" : ""}`}>
          <div className="flex h-14 items-center justify-between">
            <div className="flex items-center gap-3">
              <a href="/" className="text-gray-400 hover:text-gray-600 text-sm">
                Flow
              </a>
              <span className="text-gray-300">/</span>
              <h1 className="text-lg font-semibold text-gray-900">Tasks</h1>
            </div>
            <div className="flex items-center gap-4">
              <TenantSwitcher onTenantChange={fetchTasks} />
              <span className="h-4 w-px bg-gray-200" />
              <NavLinks current="tasks" />
              <button
                onClick={() => setCreateDrawerOpen(true)}
                className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3.5 py-1.5 text-sm font-medium text-white shadow-sm hover:bg-blue-700 transition-colors"
              >
                <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                New Task
              </button>
              <ViewToggle view={viewMode} onChange={handleViewChange} />
              <div className="text-sm text-gray-500">
                {!loading && (
                  <span>{totalCount} task{totalCount !== 1 ? "s" : ""}</span>
                )}
              </div>
            </div>
          </div>
        </div>
      </header>

      {localMode && (
        <div className="border-b border-amber-200 bg-amber-50 px-4 py-2 text-center">
          <p className="text-sm text-amber-800">
            <span className="font-medium">Local mode</span> — backend unavailable, tasks are stored in memory
          </p>
        </div>
      )}

      <main className={`mx-auto px-4 py-6 sm:px-6 lg:px-8 ${viewMode === "list" ? "max-w-7xl" : ""}`}>
        <div className="mb-4 flex items-end gap-3">
          <ProductFilter
            value={filters.productKey ?? ""}
            onChange={handleProductFilterChange}
            label="Product"
            className="w-56"
          />
        </div>
        <div className="mb-4">
          <TaskFilterBar filters={filters} onChange={handleFilterChange} />
        </div>

        {loading && !data && (
          <div className="flex items-center justify-center py-20">
            <div className="text-center">
              <div className="mx-auto mb-3 h-8 w-8 animate-spin rounded-full border-2 border-gray-300 border-t-blue-600" />
              <p className="text-sm text-gray-500">Loading tasks...</p>
            </div>
          </div>
        )}

        {error && !localMode && (
          <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-center">
            <p className="text-sm text-red-800 mb-3">{error}</p>
            <button
              onClick={fetchTasks}
              className="rounded bg-red-600 px-4 py-2 text-sm text-white hover:bg-red-700"
            >
              Retry
            </button>
          </div>
        )}

        {showEmptyState && viewMode === "list" && (
          <div className="rounded-lg border border-gray-200 bg-white p-12 text-center">
            <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-gray-100">
              <svg className="h-6 w-6 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 012-2h2a2 2 0 012 2M9 5h6" />
              </svg>
            </div>
            <p className="text-sm font-medium text-gray-900">No tasks found</p>
            <p className="mt-1 text-sm text-gray-500">
              {Object.values(filters).some((v) => v)
                ? "Try adjusting your filters."
                : "Click \"+ New Task\" to create your first task."}
            </p>
          </div>
        )}

        {showList && (
          <>
            <div className={loading ? "opacity-60 pointer-events-none" : ""}>
              <TaskTable
                tasks={tasks}
                sort={sort}
                onSortChange={handleSortChange}
                onRowClick={handleRowClick}
                onStatusClick={setStatusDialogTask}
              />
            </div>
            {!localMode && data && (
              <Pagination
                page={data.page}
                pageSize={data.pageSize}
                totalCount={data.totalCount}
                onPageChange={handlePageChange}
                onPageSizeChange={handlePageSizeChange}
              />
            )}
          </>
        )}

        {showBoard && (
          <div className={loading && !data ? "" : loading ? "opacity-60 pointer-events-none" : ""}>
            <BoardView
              tasks={tasks}
              onCardClick={handleRowClick}
              onMoveTask={handleMoveTask}
            />
          </div>
        )}
      </main>

      {statusDialogTask && (
        <StatusChangeDialog
          task={statusDialogTask}
          onConfirm={handleStatusChange}
          onClose={() => setStatusDialogTask(null)}
        />
      )}

      <TaskDetailDrawer
        taskId={selectedTaskId}
        localTask={selectedTaskId?.startsWith("local-") ? localTasksRef.current.find((t) => t.id === selectedTaskId) ?? null : null}
        onClose={handleDrawerClose}
        onUpdated={handleDrawerUpdated}
      />

      <CreateTaskDrawer
        open={createDrawerOpen}
        localMode={localMode}
        defaultProductKey={filters.productKey ?? DEFAULT_PRODUCT_KEY}
        onClose={() => setCreateDrawerOpen(false)}
        onSubmit={handleCreateTask}
      />
    </div>
    </ErrorBoundary>
  );
}

export default function TaskListPage() {
  return (
    <Suspense fallback={<div className="p-6 text-sm text-gray-500">Loading…</div>}>
      <TaskListPageInner />
    </Suspense>
  );
}
