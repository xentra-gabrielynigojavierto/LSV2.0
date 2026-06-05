const PLACEHOLDER_REGEX = /\{\{([\w.]+)\}\}/g;

export interface VariablesSchema {
  required?: string[];
  properties?: Record<string, { type?: string }>;
}

export interface RenderResult {
  subject: string | null;
  body: string;
  text: string | null;
}

export interface ValidationResult {
  valid: boolean;
  errors: string[];
}

export interface BrandedRenderInput {
  subjectTemplate: string | null;
  bodyTemplate: string;
  textTemplate: string | null;
  variablesSchemaJson: string | null;
}

export class TemplateRenderingService {
  validateVariables(
    schema: VariablesSchema | null,
    data: Record<string, unknown>
  ): ValidationResult {
    if (!schema) return { valid: true, errors: [] };

    const errors: string[] = [];

    if (schema.required) {
      for (const key of schema.required) {
        if (data[key] === undefined || data[key] === null) {
          errors.push(`Missing required variable: ${key}`);
        }
      }
    }

    if (schema.properties) {
      for (const [key, def] of Object.entries(schema.properties)) {
        if (data[key] === undefined) continue;
        const value = data[key];
        if (def.type) {
          const actualType = typeof value;
          if (def.type === "string" && actualType !== "string") {
            errors.push(`Variable '${key}' must be a string, got ${actualType}`);
          } else if (def.type === "number" && actualType !== "number") {
            errors.push(`Variable '${key}' must be a number, got ${actualType}`);
          } else if (def.type === "boolean" && actualType !== "boolean") {
            errors.push(`Variable '${key}' must be a boolean, got ${actualType}`);
          }
        }
      }
    }

    return { valid: errors.length === 0, errors };
  }

  renderTemplate(template: string, data: Record<string, unknown>): string {
    return template.replace(PLACEHOLDER_REGEX, (_match, key: string) => {
      const value = data[key];
      if (value === undefined || value === null) {
        return `{{${key}}}`;
      }
      return String(value);
    });
  }

  checkUnresolved(rendered: string): string[] {
    const unresolved: string[] = [];
    let match: RegExpExecArray | null;
    const re = /\{\{([\w.]+)\}\}/g;
    while ((match = re.exec(rendered)) !== null) {
      if (!unresolved.includes(match[1]!)) unresolved.push(match[1]!);
    }
    return unresolved;
  }

  render(
    input: {
      subjectTemplate: string | null;
      bodyTemplate: string;
      textTemplate: string | null;
      variablesSchemaJson: string | null;
    },
    data: Record<string, unknown>
  ): { result: RenderResult; errors: string[] } {
    let schema: VariablesSchema | null = null;
    if (input.variablesSchemaJson) {
      try {
        schema = JSON.parse(input.variablesSchemaJson) as VariablesSchema;
      } catch {
        return { result: { subject: null, body: "", text: null }, errors: ["variablesSchemaJson is not valid JSON"] };
      }
    }

    const validation = this.validateVariables(schema, data);
    if (!validation.valid) {
      return { result: { subject: null, body: "", text: null }, errors: validation.errors };
    }

    const body = this.renderTemplate(input.bodyTemplate, data);
    const subject = input.subjectTemplate ? this.renderTemplate(input.subjectTemplate, data) : null;
    const text = input.textTemplate ? this.renderTemplate(input.textTemplate, data) : null;

    const errors: string[] = [];
    const unresolved = [
      ...this.checkUnresolved(body),
      ...(subject ? this.checkUnresolved(subject) : []),
      ...(text ? this.checkUnresolved(text) : []),
    ];

    if (unresolved.length > 0) {
      const unique = [...new Set(unresolved)];
      return {
        result: { subject: null, body: "", text: null },
        errors: unique.map((k) => `Unresolved placeholder: {{${k}}}`),
      };
    }

    return { result: { subject, body, text }, errors: [] };
  }

  renderBranded(
    input: BrandedRenderInput,
    templateData: Record<string, unknown>,
    brandingTokens: Record<string, string>
  ): { result: RenderResult; errors: string[] } {
    const mergedData: Record<string, unknown> = {};

    for (const [key, value] of Object.entries(templateData)) {
      if (!key.startsWith("brand.")) {
        mergedData[key] = value;
      }
    }

    for (const [key, value] of Object.entries(brandingTokens)) {
      mergedData[key] = value;
    }

    return this.render(input, mergedData);
  }
}

export const templateRenderingService = new TemplateRenderingService();
