import { Op } from "sequelize";
import { Template, TemplateStatus } from "../models/template.model";
import { TemplateVersion, TemplateVersionStatus } from "../models/template-version.model";
import { NotificationChannel, ProductType, TemplateScope, EditorType } from "../types";

export class TemplateRepository {
  async findById(id: string): Promise<Template | null> {
    return Template.findByPk(id);
  }

  async findByKey(
    templateKey: string,
    channel: NotificationChannel,
    tenantId: string | null
  ): Promise<Template | null> {
    return Template.findOne({
      where: { templateKey, channel, tenantId },
    });
  }

  async findGlobalByProductKey(
    productType: ProductType,
    channel: NotificationChannel,
    templateKey: string,
    templateScope: TemplateScope = "global"
  ): Promise<Template | null> {
    return Template.findOne({
      where: { productType, channel, templateKey, templateScope },
    });
  }

  async create(input: {
    tenantId: string | null;
    templateKey: string;
    channel: NotificationChannel;
    name: string;
    description?: string | null;
    isSystemTemplate?: boolean;
    productType?: ProductType | null;
    templateScope?: TemplateScope;
    editorType?: EditorType;
    category?: string | null;
    isBrandable?: boolean;
  }): Promise<Template> {
    return Template.create({
      ...input,
      tenantId: input.tenantId ?? null,
      description: input.description ?? null,
      status: "active",
      isSystemTemplate: input.isSystemTemplate ?? false,
      productType: input.productType ?? null,
      templateScope: input.templateScope ?? "global",
      editorType: input.editorType ?? "html",
      category: input.category ?? null,
      isBrandable: input.isBrandable ?? false,
    });
  }

  async update(
    id: string,
    input: {
      name?: string;
      description?: string | null;
      status?: TemplateStatus;
      category?: string | null;
      isBrandable?: boolean;
      editorType?: EditorType;
    }
  ): Promise<void> {
    await Template.update(input, { where: { id } });
  }

  async list(filter: {
    tenantId?: string | null;
    channel?: NotificationChannel;
    status?: TemplateStatus;
    limit?: number;
    offset?: number;
  }): Promise<{ rows: Template[]; count: number }> {
    const where: Record<string, unknown> = {};
    if (filter.tenantId !== undefined) where["tenantId"] = filter.tenantId;
    if (filter.channel) where["channel"] = filter.channel;
    if (filter.status) where["status"] = filter.status;

    const limit = Math.min(filter.limit ?? 20, 100);
    const offset = filter.offset ?? 0;

    return Template.findAndCountAll({ where, limit, offset, order: [["createdAt", "DESC"]] });
  }

  async listGlobal(filter: {
    productType?: ProductType;
    channel?: NotificationChannel;
    templateKey?: string;
    status?: TemplateStatus;
    limit?: number;
    offset?: number;
  }): Promise<{ rows: Template[]; count: number }> {
    const where: Record<string, unknown> = {
      templateScope: "global",
      tenantId: null,
    };
    if (filter.productType) where["productType"] = filter.productType;
    if (filter.channel) where["channel"] = filter.channel;
    if (filter.templateKey) where["templateKey"] = filter.templateKey;
    if (filter.status) where["status"] = filter.status;

    const limit = Math.min(filter.limit ?? 20, 100);
    const offset = filter.offset ?? 0;

    return Template.findAndCountAll({ where, limit, offset, order: [["createdAt", "DESC"]] });
  }
}

export class TemplateVersionRepository {
  async findById(id: string): Promise<TemplateVersion | null> {
    return TemplateVersion.findByPk(id);
  }

  async findByTemplateId(templateId: string): Promise<TemplateVersion[]> {
    return TemplateVersion.findAll({
      where: { templateId },
      order: [["versionNumber", "DESC"]],
    });
  }

  async findPublishedByTemplateId(templateId: string): Promise<TemplateVersion | null> {
    return TemplateVersion.findOne({ where: { templateId, status: "published" } });
  }

  async countByTemplateId(templateId: string): Promise<number> {
    return TemplateVersion.count({ where: { templateId } });
  }

  async create(input: {
    templateId: string;
    subjectTemplate?: string | null;
    bodyTemplate: string;
    textTemplate?: string | null;
    variablesSchemaJson?: string | null;
    sampleDataJson?: string | null;
    editorJson?: string | null;
    designTokensJson?: string | null;
    layoutType?: string | null;
  }): Promise<TemplateVersion> {
    const count = await this.countByTemplateId(input.templateId);
    return TemplateVersion.create({
      ...input,
      versionNumber: count + 1,
      status: "draft",
      publishedAt: null,
    });
  }

  async publish(templateId: string, versionId: string): Promise<void> {
    await TemplateVersion.update(
      { status: "retired" as TemplateVersionStatus },
      { where: { templateId, status: "published" } }
    );
    await TemplateVersion.update(
      { status: "published" as TemplateVersionStatus, publishedAt: new Date() },
      { where: { id: versionId, templateId } }
    );
  }
}
