import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, test, expect, beforeEach } from 'vitest';
import { AttachmentPanel } from '../attachment-panel';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';

vi.mock('@/lib/careconnect-api', () => ({
  careConnectApi: {
    referralAttachments: {
      list:         vi.fn(),
      upload:       vi.fn(),
      getSignedUrl: vi.fn(),
    },
    appointmentAttachments: {
      list:         vi.fn(),
      upload:       vi.fn(),
      getSignedUrl: vi.fn(),
    },
  },
}));

// ATT_1 is older, ATT_2 is newer — component sorts newest-first by default.
const ATT_1 = {
  id:            'att-1',
  fileName:      'referral-notes.pdf',
  contentType:   'application/pdf',
  fileSizeBytes: 204800,
  status:        'available',
  createdAtUtc:  '2026-01-15T10:00:00Z',
};

const ATT_2 = {
  id:            'att-2',
  fileName:      'lab-results.png',
  contentType:   'image/png',
  fileSizeBytes: 512000,
  status:        'available',
  createdAtUtc:  '2026-01-16T09:30:00Z',
};

const SIGNED_URL = {
  url:              'https://storage.example.com/signed?token=abc',
  expiresInSeconds: 300,
};

function ok<T>(data: T) {
  return { data, correlationId: 'c', status: 200 } as const;
}

function makeApiError(status: number, message: string): ApiError {
  return new ApiError(status, message, 'test-corr');
}

const mockList      = () => vi.mocked(careConnectApi.referralAttachments.list);
const mockUpload    = () => vi.mocked(careConnectApi.referralAttachments.upload);
const mockSignedUrl = () => vi.mocked(careConnectApi.referralAttachments.getSignedUrl);

describe('AttachmentPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(window, 'open').mockReturnValue(null);
  });

  // ── List on mount ────────────────────────────────────────────────────────

  test('shows "No documents uploaded yet." when the list is empty', async () => {
    mockList().mockResolvedValue(ok([]));
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);
    await waitFor(() =>
      expect(screen.getByText('No documents uploaded yet.')).toBeInTheDocument(),
    );
  });

  test('renders all attachment file names returned on mount', async () => {
    mockList().mockResolvedValue(ok([ATT_1, ATT_2]));
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);
    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );
    expect(screen.getByText('lab-results.png')).toBeInTheDocument();
  });

  test('calls list() once on mount with the correct entityId', async () => {
    mockList().mockResolvedValue(ok([]));
    render(<AttachmentPanel entityType="referral" entityId="ref-99" />);
    await waitFor(() => expect(mockList()).toHaveBeenCalledTimes(1));
    expect(mockList()).toHaveBeenCalledWith('ref-99');
  });

  test('shows a load-error banner when listing fails', async () => {
    mockList().mockRejectedValue(makeApiError(500, 'Database unavailable'));
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);
    await waitFor(() =>
      expect(screen.getByText('Database unavailable')).toBeInTheDocument(),
    );
  });

  // ── Upload success ────────────────────────────────────────────────────────

  test('adds the new attachment to the list after a successful upload', async () => {
    mockList().mockResolvedValue(ok([ATT_1]));
    mockUpload().mockResolvedValue(ok(ATT_2));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" canUpload />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );

    const file = new File(['content'], 'lab-results.png', { type: 'image/png' });
    await user.upload(screen.getByLabelText('+ Upload'), file);

    await waitFor(() =>
      expect(screen.getByText('lab-results.png')).toBeInTheDocument(),
    );
    expect(mockUpload()).toHaveBeenCalledWith('ref-1', file);
  });

  test('upload button label returns to "+ Upload" after upload completes', async () => {
    mockList().mockResolvedValue(ok([]));
    mockUpload().mockResolvedValue(ok(ATT_1));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" canUpload />);

    await waitFor(() =>
      expect(screen.getByText('No documents uploaded yet.')).toBeInTheDocument(),
    );

    const file = new File(['x'], 'doc.pdf', { type: 'application/pdf' });
    await user.upload(screen.getByLabelText('+ Upload'), file);

    // After upload completes the label should revert (not show "Uploading…")
    await waitFor(() =>
      expect(screen.queryByText('Uploading…')).not.toBeInTheDocument(),
    );
  });

  // ── Upload failure ────────────────────────────────────────────────────────

  test('shows the ApiError message in the error banner when upload fails with 413', async () => {
    mockList().mockResolvedValue(ok([]));
    mockUpload().mockRejectedValue(makeApiError(413, 'File size exceeds the 10 MB limit.'));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" canUpload />);

    await waitFor(() =>
      expect(screen.getByText('No documents uploaded yet.')).toBeInTheDocument(),
    );

    await user.upload(
      screen.getByLabelText('+ Upload'),
      new File(['x'], 'huge.pdf', { type: 'application/pdf' }),
    );

    await waitFor(() =>
      expect(
        screen.getByText('File size exceeds the 10 MB limit.'),
      ).toBeInTheDocument(),
    );
  });

  test('shows the generic upload-error fallback when a non-ApiError is thrown', async () => {
    mockList().mockResolvedValue(ok([]));
    mockUpload().mockRejectedValue(new TypeError('Network error'));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" canUpload />);

    await waitFor(() =>
      expect(screen.getByText('No documents uploaded yet.')).toBeInTheDocument(),
    );

    await user.upload(
      screen.getByLabelText('+ Upload'),
      new File(['x'], 'doc.pdf', { type: 'application/pdf' }),
    );

    await waitFor(() =>
      expect(screen.getByText('Upload failed. Please try again.')).toBeInTheDocument(),
    );
  });

  // ── View button — signed URL ──────────────────────────────────────────────

  test('clicking View fetches a fresh signed URL and opens it in a new tab', async () => {
    mockList().mockResolvedValue(ok([ATT_1]));
    mockSignedUrl().mockResolvedValue(ok(SIGNED_URL));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );

    await user.click(screen.getByRole('button', { name: 'View' }));

    await waitFor(() =>
      expect(mockSignedUrl()).toHaveBeenCalledWith('ref-1', 'att-1'),
    );
    expect(window.open).toHaveBeenCalledWith(
      SIGNED_URL.url, '_blank', 'noopener,noreferrer',
    );
  });

  test('each View click triggers a new network request (fresh URL per click)', async () => {
    mockList().mockResolvedValue(ok([ATT_1]));
    mockSignedUrl()
      .mockResolvedValueOnce(ok({ url: 'https://x.com/url1', expiresInSeconds: 300 }))
      .mockResolvedValueOnce(ok({ url: 'https://x.com/url2', expiresInSeconds: 300 }));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );

    const viewBtn = screen.getByRole('button', { name: 'View' });
    await user.click(viewBtn);
    await waitFor(() => expect(mockSignedUrl()).toHaveBeenCalledTimes(1));

    await user.click(viewBtn);
    await waitFor(() => expect(mockSignedUrl()).toHaveBeenCalledTimes(2));

    expect(window.open).toHaveBeenCalledTimes(2);
  });

  // ── View button — 403 ────────────────────────────────────────────────────

  test('shows the permission-denied message when View returns 403', async () => {
    mockList().mockResolvedValue(ok([ATT_1]));
    mockSignedUrl().mockRejectedValue(makeApiError(403, 'Forbidden'));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );

    await user.click(screen.getByRole('button', { name: 'View' }));

    await waitFor(() =>
      expect(
        screen.getByText('You do not have permission to view this document.'),
      ).toBeInTheDocument(),
    );
    expect(window.open).not.toHaveBeenCalled();
  });

  // ── View button — 503 ────────────────────────────────────────────────────

  test('shows the temporarily-unavailable message when View returns 503', async () => {
    mockList().mockResolvedValue(ok([ATT_1]));
    mockSignedUrl().mockRejectedValue(makeApiError(503, 'Service Unavailable'));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );

    await user.click(screen.getByRole('button', { name: 'View' }));

    await waitFor(() =>
      expect(
        screen.getByText('The document is temporarily unavailable. Try again shortly.'),
      ).toBeInTheDocument(),
    );
    expect(window.open).not.toHaveBeenCalled();
  });

  test('shows err.message for non-403/503 ApiErrors on View', async () => {
    mockList().mockResolvedValue(ok([ATT_1]));
    mockSignedUrl().mockRejectedValue(makeApiError(404, 'Attachment not found.'));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );

    await user.click(screen.getByRole('button', { name: 'View' }));

    await waitFor(() =>
      expect(screen.getByText('Attachment not found.')).toBeInTheDocument(),
    );
  });

  test('shows the generic fallback when a non-ApiError is thrown on View', async () => {
    mockList().mockResolvedValue(ok([ATT_1]));
    mockSignedUrl().mockRejectedValue(new TypeError('Network failure'));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );

    await user.click(screen.getByRole('button', { name: 'View' }));

    await waitFor(() =>
      expect(
        screen.getByText('Unable to open the document. Please try again.'),
      ).toBeInTheDocument(),
    );
  });

  // ── Per-row error isolation ───────────────────────────────────────────────

  test('view error is scoped to the clicked row and does not appear on other rows', async () => {
    mockList().mockResolvedValue(ok([ATT_1, ATT_2]));
    mockSignedUrl().mockRejectedValue(makeApiError(403, 'Forbidden'));

    const user = userEvent.setup();
    render(<AttachmentPanel entityType="referral" entityId="ref-1" />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );

    // Target ATT_1's row specifically (ATT_2 is first due to newest-first sort)
    const att1Row = screen.getByText('referral-notes.pdf').closest('li')!;
    await user.click(within(att1Row).getByRole('button', { name: 'View' }));

    await waitFor(() =>
      expect(
        within(att1Row).getByText('You do not have permission to view this document.'),
      ).toBeInTheDocument(),
    );

    const att2Row = screen.getByText('lab-results.png').closest('li')!;
    expect(
      within(att2Row).queryByText('You do not have permission to view this document.'),
    ).not.toBeInTheDocument();
  });

  // ── Appointment entity type ───────────────────────────────────────────────

  test('routes to appointmentAttachments API when entityType is "appointment"', async () => {
    const apptList = vi.mocked(careConnectApi.appointmentAttachments.list);
    apptList.mockResolvedValue(ok([ATT_1]));

    render(<AttachmentPanel entityType="appointment" entityId="appt-55" />);

    await waitFor(() =>
      expect(screen.getByText('referral-notes.pdf')).toBeInTheDocument(),
    );
    expect(apptList).toHaveBeenCalledWith('appt-55');
    expect(mockList()).not.toHaveBeenCalled();
  });
});
