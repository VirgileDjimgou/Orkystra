import { sendApiRequest } from './apiClient'

export type AiEvidenceView = {
  source: string
  detail: string
}

export type AiRecommendedActionView = {
  title: string
  rationale: string
  priority: string
}

export type AiRecommendationView = {
  intent: 'warehouse' | 'dispatcher' | 'unknown'
  directAnswer: string
  evidence: AiEvidenceView[]
  assumptions: string[]
  recommendedActions: AiRecommendedActionView[]
  confidenceLevel: 'high' | 'medium' | 'low'
  alternativeScenarioNote: string | null
  missingData: string[]
  specialistAgents: string[]
}

export type AiRecommendationEnvelopeView = {
  recommendation: AiRecommendationView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export type AiRecommendationInput = {
  question: string
  scenarioId?: string | null
}

export type AiRecommendationLoadResult = {
  workflow: AiRecommendationEnvelopeView
  source: 'api' | 'fallback'
  errorMessage: string | null
}

export async function loadAiRecommendation(input: AiRecommendationInput): Promise<AiRecommendationLoadResult> {
  try {
    const response = await sendApiRequest('/api/ai/recommendations', {
      method: 'POST',
      includeTenantHeader: true,
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(input),
    })

    if (!response.ok) {
      throw new Error(`AI workflow request failed with status ${response.status}`)
    }

    const workflow = (await response.json()) as AiRecommendationEnvelopeView

    return {
      workflow,
      source: workflow.source,
      errorMessage: workflow.errorMessage,
    }
  } catch (error) {
    return {
      workflow: {
        recommendation: {
          intent: 'unknown',
          directAnswer: '',
          evidence: [],
          assumptions: [],
          recommendedActions: [],
          confidenceLevel: 'low',
          alternativeScenarioNote: null,
          missingData: ['AI workflow endpoint unavailable'],
          specialistAgents: ['frontend-fallback'],
        },
        source: 'fallback',
        errorMessage: error instanceof Error ? error.message : 'AI workflow request failed.',
      },
      source: 'fallback',
      errorMessage: error instanceof Error ? error.message : 'AI workflow request failed.',
    }
  }
}
