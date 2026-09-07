import { api } from "@/lib/api/client";
import { toQuery } from "@/lib/query";
import type { AuditLog, PagedResult } from "@/lib/types";

export function listAuditLogs(params: {
  entityType?: string;
  actorUserId?: string;
  page?: number;
  pageSize?: number;
} = {}) {
  return api.get<PagedResult<AuditLog>>(
    `/admin/audit-logs${toQuery({
      entityType: params.entityType,
      actorUserId: params.actorUserId,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    })}`,
  );
}
