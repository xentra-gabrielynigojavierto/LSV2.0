import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';


/**
 * /support/customer — redirect entry point.
 * External customers land here and are forwarded to the ticket list.
 */
export default function CustomerSupportRootPage() {
  redirect('/support/customer/tickets');
}
