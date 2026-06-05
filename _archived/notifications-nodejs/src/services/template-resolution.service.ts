import { Template } from "../models/template.model";
import { TemplateVersion } from "../models/template-version.model";
import { TemplateRepository, TemplateVersionRepository } from "../repositories/template.repository";
import { NotificationChannel, ProductType } from "../types";
import { logger } from "../shared/logger";

export interface ResolvedTemplate {
  template: Template;
  version: TemplateVersion;
}

export class TemplateResolutionService {
  private templateRepo: TemplateRepository;
  private versionRepo: TemplateVersionRepository;

  constructor() {
    this.templateRepo = new TemplateRepository();
    this.versionRepo = new TemplateVersionRepository();
  }

  /**
   * Resolution order:
   * 1. Tenant-specific active template with a published version
   * 2. Global (tenantId=null) system template with a published version
   * 3. Fail cleanly
   */
  async resolve(
    tenantId: string,
    templateKey: string,
    channel: NotificationChannel
  ): Promise<ResolvedTemplate | null> {
    const tenantTemplate = await this.templateRepo.findByKey(templateKey, channel, tenantId);
    if (tenantTemplate && tenantTemplate.status === "active") {
      const version = await this.versionRepo.findPublishedByTemplateId(tenantTemplate.id);
      if (version) {
        logger.debug("Template resolved via tenant-specific override", {
          tenantId,
          templateKey,
          channel,
          templateId: tenantTemplate.id,
          versionId: version.id,
        });
        return { template: tenantTemplate, version };
      }
    }

    const globalTemplate = await this.templateRepo.findByKey(templateKey, channel, null);
    if (globalTemplate && globalTemplate.status === "active") {
      const version = await this.versionRepo.findPublishedByTemplateId(globalTemplate.id);
      if (version) {
        logger.debug("Template resolved via global system template", {
          tenantId,
          templateKey,
          channel,
          templateId: globalTemplate.id,
          versionId: version.id,
        });
        return { template: globalTemplate, version };
      }
    }

    logger.warn("Template resolution failed — no active+published template found", {
      tenantId,
      templateKey,
      channel,
    });
    return null;
  }

  async resolveByProduct(
    tenantId: string,
    templateKey: string,
    channel: NotificationChannel,
    productType: ProductType
  ): Promise<ResolvedTemplate | null> {
    const globalTemplate = await this.templateRepo.findGlobalByProductKey(
      productType,
      channel,
      templateKey,
      "global"
    );
    if (globalTemplate && globalTemplate.status === "active") {
      const version = await this.versionRepo.findPublishedByTemplateId(globalTemplate.id);
      if (version) {
        logger.debug("Template resolved via product-type global template", {
          tenantId,
          templateKey,
          channel,
          productType,
          templateId: globalTemplate.id,
          versionId: version.id,
        });
        return { template: globalTemplate, version };
      }
    }

    return this.resolve(tenantId, templateKey, channel);
  }
}
