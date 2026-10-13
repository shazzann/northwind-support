import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalateTicketPayload, EscalationResult, TicketDetail } from '../../../types/api';
import {
  type EscalationErrors,
  ESCALATED_BY_MAX_LENGTH,
  escalationBlocker,
  nextPriority,
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  validateEscalation,
} from '../escalation';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (result: EscalationResult) => void;
}

const emptyForm: EscalateTicketPayload = { reason: '', escalatedBy: '' };

/**
 * Escalates a ticket. Hidden behind an explanation when the rules would refuse, but still
 * shows the server's answer if it refuses anyway (someone else may have got there first).
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [form, setForm] = useState<EscalateTicketPayload>(emptyForm);
  const [errors, setErrors] = useState<EscalationErrors>({});
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  const blocker = escalationBlocker(ticket);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const payload: EscalateTicketPayload = {
      reason: form.reason.trim(),
      escalatedBy: form.escalatedBy.trim(),
    };
    const validationErrors = validateEscalation(payload);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      const result = await ticketsApi.escalate(ticket.id, payload);
      setForm(emptyForm);
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setErrors(fromApiErrors(caught.fieldErrors));
      }

      setSubmitError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <section className="card" aria-labelledby="escalation-heading">
      <h2 id="escalation-heading">Escalate</h2>

      {blocker ? (
        <p className="field__hint">{blocker}</p>
      ) : (
        <form className="form" onSubmit={submit} noValidate>
          <p className="field__hint">
            Raises the priority to {nextPriority(ticket.priority)}, starts a new SLA window from now
            and re-checks who should own it.
          </p>

          <Field
            id="escalation-reason"
            label="Reason"
            error={errors.reason}
            hint={`${REASON_MIN_LENGTH} to ${REASON_MAX_LENGTH} characters (${form.reason.trim().length} so far).`}
          >
            {(fieldProps) => (
              <textarea
                {...fieldProps}
                rows={3}
                value={form.reason}
                maxLength={REASON_MAX_LENGTH}
                onChange={(event) => setForm((current) => ({ ...current, reason: event.target.value }))}
              />
            )}
          </Field>

          <Field id="escalation-by" label="Escalated by" error={errors.escalatedBy}>
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="text"
                value={form.escalatedBy}
                maxLength={ESCALATED_BY_MAX_LENGTH}
                onChange={(event) => setForm((current) => ({ ...current, escalatedBy: event.target.value }))}
              />
            )}
          </Field>

          <div className="button-row">
            <button type="submit" className="button button--primary" disabled={isSaving}>
              {isSaving ? 'Escalating...' : `Escalate to ${nextPriority(ticket.priority)}`}
            </button>
          </div>
        </form>
      )}

      {submitError && (
        <p className="field__error" role="alert">
          {submitError}
        </p>
      )}
    </section>
  );
}

/** The API names fields as C# does ("Reason"); the form uses camelCase. */
function fromApiErrors(fieldErrors: Record<string, string[]>): EscalationErrors {
  return {
    reason: fieldErrors.Reason?.[0],
    escalatedBy: fieldErrors.EscalatedBy?.[0],
  };
}
