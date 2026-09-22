import { describe, expect, it } from "vitest";
import {
  DRIVER_ELIGIBILITY_LABEL,
  DRIVER_ONBOARDING_LABEL,
  OCCURRENCE_TYPE_LABEL,
  labelOf,
} from "./labels";

describe("labelOf", () => {
  it("mapeia onboardingStatus para rótulos em português", () => {
    expect(labelOf(DRIVER_ONBOARDING_LABEL, "PendingDocuments")).toBe("Documentação pendente");
    expect(labelOf(DRIVER_ONBOARDING_LABEL, "UnderReview")).toBe("Em análise");
    expect(labelOf(DRIVER_ONBOARDING_LABEL, "Rejected")).toBe("Reprovado");
  });

  it("mapeia eligibilityStatus, incluindo DocumentExpired", () => {
    expect(labelOf(DRIVER_ELIGIBILITY_LABEL, "DocumentExpired")).toBe("CNH vencida");
    expect(labelOf(DRIVER_ELIGIBILITY_LABEL, "Eligible")).toBe("Apto a trabalhar");
  });

  it("nunca mostra o enum técnico cru quando existe rótulo", () => {
    for (const value of Object.keys(OCCURRENCE_TYPE_LABEL)) {
      expect(labelOf(OCCURRENCE_TYPE_LABEL, value)).not.toBe(value);
    }
  });

  it("faz fallback para o próprio valor quando desconhecido", () => {
    expect(labelOf(DRIVER_ONBOARDING_LABEL, "SomethingNew")).toBe("SomethingNew");
  });
});
