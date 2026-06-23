import { sendApiRequest } from './apiClient'

export type TransportExceptionFollowUpTransitionInput = {
  action: 'retire' | 'reopen'
  exceptionId: string
  note: string
}

export async function transitionTransportExceptionFollowUp(
  input: TransportExceptionFollowUpTransitionInput
): Promise<void> {
  const response = await sendApiRequest(
    `/api/transport/exceptions-workbench/follow-up-queue/${input.exceptionId}`,
    {
      method: 'PUT',
      includeTenantHeader: true,
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        action: input.action,
        note: input.note,
      }),
    }
  )

  if (!response.ok) {
    throw new Error(`Transport exception follow-up transition failed with status ${response.status}`)
  }
}
