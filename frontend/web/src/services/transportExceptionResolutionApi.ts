import { sendApiRequest } from './apiClient'

export type SaveTransportExceptionResolutionInput = {
  exceptionId: string
  status: 'Reviewed' | 'Resolved' | 'Deferred'
  note: string
  followUpOwner: string
  targetReturnAtUtc: string | null
}

export async function saveTransportExceptionResolution(
  input: SaveTransportExceptionResolutionInput
): Promise<void> {
  const response = await sendApiRequest('/api/transport/exceptions-workbench/resolutions', {
    method: 'PUT',
    includeTenantHeader: true,
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      exceptionId: input.exceptionId,
      status: input.status,
      note: input.note,
      followUpOwner: input.followUpOwner,
      targetReturnAtUtc: input.targetReturnAtUtc,
    }),
  })

  if (!response.ok) {
    throw new Error(`Transport exception resolution request failed with status ${response.status}`)
  }
}
