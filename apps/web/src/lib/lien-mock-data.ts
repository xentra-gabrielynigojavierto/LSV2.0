import type {
  CaseSummary, CaseDetail, LienSummary, LienDetail, BillOfSaleSummary, BillOfSaleDetail,
  ServicingItem, ServicingDetail, ContactSummary, ContactDetail,
  DocumentSummary, DocumentDetail, LienUser, LienUserDetail,
  LienOfferSummary, LienStatusHistoryItem,
} from '@/types/lien';

export const MOCK_CASES: CaseSummary[] = [
  { id: 'c001', caseNumber: 'CASE-2024-0001', status: 'PreDemand', clientName: 'Maria Gonzalez', lawFirm: 'Cortex TBI of Nevada', medicalFacility: 'Desert Springs Medical', dateOfIncident: '2024-03-15', totalLienAmount: 45000, lienCount: 3, assignedTo: 'Sarah Chen', createdAtUtc: '2024-03-20T10:00:00Z', updatedAtUtc: '2024-11-01T14:30:00Z' },
  { id: 'c002', caseNumber: 'CASE-2024-0002', status: 'DemandSent', clientName: 'James Thompson', lawFirm: 'Cortex TBI of Nevada', medicalFacility: 'Valley Health System', dateOfIncident: '2024-01-08', totalLienAmount: 78500, lienCount: 5, assignedTo: 'Michael Park', createdAtUtc: '2024-01-15T09:00:00Z', updatedAtUtc: '2024-10-28T11:00:00Z' },
  { id: 'c003', caseNumber: 'CASE-2024-0003', status: 'InNegotiation', clientName: 'Angela Davis', lawFirm: 'QA Lawfirm', medicalFacility: 'Sunrise Hospital', dateOfIncident: '2024-05-22', totalLienAmount: 125000, lienCount: 8, assignedTo: 'Sarah Chen', createdAtUtc: '2024-05-30T08:00:00Z', updatedAtUtc: '2024-11-02T16:45:00Z' },
  { id: 'c004', caseNumber: 'CASE-2024-0004', status: 'CaseSettled', clientName: 'Robert Kim', lawFirm: 'Cortex QA LawFirm', medicalFacility: 'UMC Trauma Center', dateOfIncident: '2023-11-10', totalLienAmount: 234000, lienCount: 12, assignedTo: 'Lisa Wang', createdAtUtc: '2023-11-20T07:30:00Z', updatedAtUtc: '2024-09-15T10:00:00Z' },
  { id: 'c005', caseNumber: 'CASE-2024-0005', status: 'PreDemand', clientName: 'Patricia Hernandez', lawFirm: 'Cortex TBI of Nevada', medicalFacility: 'Henderson Hospital', dateOfIncident: '2024-07-03', totalLienAmount: 32000, lienCount: 2, assignedTo: 'Michael Park', createdAtUtc: '2024-07-10T11:00:00Z', updatedAtUtc: '2024-10-20T09:15:00Z' },
  { id: 'c006', caseNumber: 'CASE-2024-0006', status: 'PreDemand', clientName: 'David Johnson', lawFirm: 'QA Lawfirm', medicalFacility: 'Spring Valley Hospital', dateOfIncident: '2024-08-19', totalLienAmount: 56750, lienCount: 4, assignedTo: 'Sarah Chen', createdAtUtc: '2024-08-25T13:00:00Z', updatedAtUtc: '2024-11-01T08:00:00Z' },
  { id: 'c007', caseNumber: 'CASE-2024-0007', status: 'Closed', clientName: 'Susan Williams', lawFirm: 'Cortex TBI of Nevada', medicalFacility: 'Desert Springs Medical', dateOfIncident: '2023-09-05', totalLienAmount: 89000, lienCount: 6, assignedTo: 'Lisa Wang', createdAtUtc: '2023-09-12T10:00:00Z', updatedAtUtc: '2024-06-30T15:00:00Z' },
  { id: 'c008', caseNumber: 'CASE-2024-0008', status: 'PreDemand', clientName: 'Michael Brown', lawFirm: 'Cortex QA LawFirm', medicalFacility: 'Valley Health System', dateOfIncident: '2024-09-28', totalLienAmount: 18500, lienCount: 1, assignedTo: 'Michael Park', createdAtUtc: '2024-10-05T14:00:00Z', updatedAtUtc: '2024-10-30T12:00:00Z' },
];

export const MOCK_CASE_DETAILS: Record<string, CaseDetail> = {
  c001: { ...MOCK_CASES[0], description: 'Motor vehicle accident on I-15. Client sustained TBI and multiple fractures.', clientDob: '1985-06-12', clientPhone: '(702) 555-0101', clientEmail: 'mgonzalez@email.com', clientAddress: '4521 Desert Rose Dr, Las Vegas, NV 89120', insuranceCarrier: 'State Farm', policyNumber: 'SF-2024-98765', claimNumber: 'CLM-2024-45678', notes: 'Client referred by Dr. Martinez. Multiple treatment facilities involved.' },
  c002: { ...MOCK_CASES[1], description: 'Workplace injury at construction site. Spinal cord compression.', clientDob: '1978-11-03', clientPhone: '(702) 555-0202', clientEmail: 'jthompson@email.com', clientAddress: '892 Palm Ave, Henderson, NV 89015', insuranceCarrier: 'GEICO', policyNumber: 'GK-2024-34567', claimNumber: 'CLM-2024-78901', demandAmount: 150000, notes: 'Complex multi-party liability. OSHA investigation pending.' },
  c003: { ...MOCK_CASES[2], description: 'Slip and fall at commercial property. Bilateral hip replacement required.', clientDob: '1962-03-28', clientPhone: '(702) 555-0303', clientEmail: 'adavis@email.com', clientAddress: '2100 Spring Mountain Rd, Las Vegas, NV 89102', insuranceCarrier: 'Progressive', policyNumber: 'PG-2024-56789', claimNumber: 'CLM-2024-23456', demandAmount: 250000, notes: 'Surveillance footage obtained. Liability established.' },
  c004: { ...MOCK_CASES[3], description: 'Multi-vehicle pileup on US-95. TBI with extended ICU stay.', clientDob: '1990-08-17', clientPhone: '(702) 555-0404', clientEmail: 'rkim@email.com', clientAddress: '7730 W Flamingo Rd, Las Vegas, NV 89147', insuranceCarrier: 'Allstate', policyNumber: 'AS-2023-12345', claimNumber: 'CLM-2023-67890', demandAmount: 450000, settlementAmount: 385000, notes: 'Settlement reached. Lien resolution in progress.' },
};

export const MOCK_LIENS: LienSummary[] = [
  { id: 'l001', tenantId: 't1', lienNumber: 'LN-2024-0001', lienType: 'MedicalLien', status: 'Offered', originalAmount: 15000, offerPrice: 12000, jurisdiction: 'Nevada', caseRef: 'CASE-2024-0001', isConfidential: false, subjectParty: { firstName: 'Maria', lastName: 'Gonzalez' }, sellingOrg: { orgId: 'o1', orgName: 'Desert Springs Medical' }, createdAtUtc: '2024-03-25T10:00:00Z', updatedAtUtc: '2024-10-15T14:00:00Z' },
  { id: 'l002', tenantId: 't1', lienNumber: 'LN-2024-0002', lienType: 'MedicalLien', status: 'Draft', originalAmount: 22000, jurisdiction: 'Nevada', caseRef: 'CASE-2024-0001', isConfidential: false, subjectParty: { firstName: 'Maria', lastName: 'Gonzalez' }, sellingOrg: { orgId: 'o2', orgName: 'Valley Orthopedics' }, createdAtUtc: '2024-04-02T09:00:00Z', updatedAtUtc: '2024-10-20T11:00:00Z' },
  { id: 'l003', tenantId: 't1', lienNumber: 'LN-2024-0003', lienType: 'AttorneyLien', status: 'Offered', originalAmount: 35000, offerPrice: 28000, jurisdiction: 'Nevada', caseRef: 'CASE-2024-0002', isConfidential: false, subjectParty: { firstName: 'James', lastName: 'Thompson' }, sellingOrg: { orgId: 'o3', orgName: 'Cortex TBI of Nevada' }, createdAtUtc: '2024-02-10T08:00:00Z', updatedAtUtc: '2024-10-28T16:00:00Z' },
  { id: 'l004', tenantId: 't1', lienNumber: 'LN-2024-0004', lienType: 'MedicalLien', status: 'Sold', originalAmount: 48000, offerPrice: 38000, purchasePrice: 36500, jurisdiction: 'Nevada', caseRef: 'CASE-2024-0003', isConfidential: true, sellingOrg: { orgId: 'o4', orgName: 'Sunrise Hospital' }, buyingOrg: { orgId: 'o5', orgName: 'MedLien Capital' }, createdAtUtc: '2024-06-15T07:30:00Z', updatedAtUtc: '2024-09-01T10:00:00Z' },
  { id: 'l005', tenantId: 't1', lienNumber: 'LN-2024-0005', lienType: 'WorkersCompLien', status: 'Draft', originalAmount: 67000, jurisdiction: 'California', caseRef: 'CASE-2024-0004', isConfidential: false, subjectParty: { firstName: 'Robert', lastName: 'Kim' }, sellingOrg: { orgId: 'o6', orgName: 'UMC Trauma Center' }, createdAtUtc: '2023-12-01T11:00:00Z', updatedAtUtc: '2024-10-05T09:00:00Z' },
  { id: 'l006', tenantId: 't1', lienNumber: 'LN-2024-0006', lienType: 'SettlementAdvance', status: 'Offered', originalAmount: 25000, offerPrice: 20000, jurisdiction: 'Nevada', caseRef: 'CASE-2024-0005', isConfidential: false, subjectParty: { firstName: 'Patricia', lastName: 'Hernandez' }, sellingOrg: { orgId: 'o1', orgName: 'Desert Springs Medical' }, createdAtUtc: '2024-07-20T13:00:00Z', updatedAtUtc: '2024-10-25T15:00:00Z' },
  { id: 'l007', tenantId: 't1', lienNumber: 'LN-2024-0007', lienType: 'MedicalLien', status: 'Withdrawn', originalAmount: 12500, jurisdiction: 'Nevada', caseRef: 'CASE-2024-0007', isConfidential: false, subjectParty: { firstName: 'Susan', lastName: 'Williams' }, sellingOrg: { orgId: 'o2', orgName: 'Valley Orthopedics' }, createdAtUtc: '2023-10-15T10:00:00Z', updatedAtUtc: '2024-03-10T14:00:00Z' },
  { id: 'l008', tenantId: 't1', lienNumber: 'LN-2024-0008', lienType: 'MedicalLien', status: 'Draft', originalAmount: 18500, jurisdiction: 'Nevada', caseRef: 'CASE-2024-0008', isConfidential: false, subjectParty: { firstName: 'Michael', lastName: 'Brown' }, sellingOrg: { orgId: 'o4', orgName: 'Sunrise Hospital' }, createdAtUtc: '2024-10-10T14:00:00Z', updatedAtUtc: '2024-10-30T12:00:00Z' },
];

export const MOCK_LIEN_DETAILS: Record<string, LienDetail> = {
  l001: {
    ...MOCK_LIENS[0],
    incidentDate: '2024-03-15', description: 'Emergency treatment and imaging for TBI. Includes MRI, CT scan, and 3-day ICU stay.',
    offerExpiresAtUtc: '2025-01-15T23:59:59Z', offerNotes: 'Standard 80% offer on medical charges.',
    offers: [
      { id: 'off001', lienId: 'l001', buyerOrgId: 'o5', buyerOrgName: 'MedLien Capital', offerAmount: 11500, notes: 'Requesting 5% additional discount', status: 'Pending', createdAtUtc: '2024-10-20T10:00:00Z', updatedAtUtc: '2024-10-20T10:00:00Z' },
    ],
  },
  l003: {
    ...MOCK_LIENS[2],
    incidentDate: '2024-01-08', description: 'Attorney fees for spinal cord injury case representation. Contingency fee arrangement.',
    offerExpiresAtUtc: '2025-02-10T23:59:59Z', offerNotes: 'Reduced rate due to volume relationship.',
    offers: [
      { id: 'off002', lienId: 'l003', buyerOrgId: 'o7', buyerOrgName: 'National Lien Buyers LLC', offerAmount: 26000, status: 'Pending', createdAtUtc: '2024-10-30T09:00:00Z', updatedAtUtc: '2024-10-30T09:00:00Z' },
      { id: 'off003', lienId: 'l003', buyerOrgId: 'o5', buyerOrgName: 'MedLien Capital', offerAmount: 27500, notes: 'Final offer', status: 'Pending', createdAtUtc: '2024-10-29T14:00:00Z', updatedAtUtc: '2024-10-29T14:00:00Z' },
    ],
  },
};

export const MOCK_LIEN_HISTORY: LienStatusHistoryItem[] = [
  { status: 'Draft', occurredAtUtc: '2024-03-25T10:00:00Z', label: 'Lien Created', actorOrgName: 'Desert Springs Medical' },
  { status: 'Offered', occurredAtUtc: '2024-09-01T14:00:00Z', label: 'Listed for Sale', actorOrgName: 'Desert Springs Medical' },
];

export const MOCK_BILLS_OF_SALE: BillOfSaleSummary[] = [
  { id: 'bos001', bosNumber: 'BOS-2024-0001', status: 'Executed', lienId: 'l004', lienNumber: 'LN-2024-0004', caseNumber: 'CASE-2024-0003', sellerOrg: 'Sunrise Hospital', buyerOrg: 'MedLien Capital', saleAmount: 36500, executionDate: '2024-09-01', createdAtUtc: '2024-08-20T10:00:00Z' },
  { id: 'bos002', bosNumber: 'BOS-2024-0002', status: 'Pending', lienId: 'l001', lienNumber: 'LN-2024-0001', caseNumber: 'CASE-2024-0001', sellerOrg: 'Desert Springs Medical', buyerOrg: 'MedLien Capital', saleAmount: 12000, createdAtUtc: '2024-10-22T09:00:00Z' },
  { id: 'bos003', bosNumber: 'BOS-2024-0003', status: 'Draft', lienId: 'l003', lienNumber: 'LN-2024-0003', caseNumber: 'CASE-2024-0002', sellerOrg: 'Cortex TBI of Nevada', buyerOrg: 'National Lien Buyers LLC', saleAmount: 28000, createdAtUtc: '2024-11-01T14:00:00Z' },
  { id: 'bos004', bosNumber: 'BOS-2024-0004', status: 'Cancelled', lienId: 'l007', lienNumber: 'LN-2024-0007', sellerOrg: 'Valley Orthopedics', buyerOrg: 'MedLien Capital', saleAmount: 10000, createdAtUtc: '2024-02-15T08:00:00Z' },
  { id: 'bos005', bosNumber: 'BOS-2024-0005', status: 'Executed', lienId: 'l006', lienNumber: 'LN-2024-0006', caseNumber: 'CASE-2024-0005', sellerOrg: 'Desert Springs Medical', buyerOrg: 'Lien Solutions Group', saleAmount: 20000, executionDate: '2024-10-28', createdAtUtc: '2024-10-15T11:00:00Z' },
];

export const MOCK_BOS_DETAILS: Record<string, BillOfSaleDetail> = {
  bos001: { ...MOCK_BILLS_OF_SALE[0], originalLienAmount: 48000, discountPercent: 23.96, sellerContact: 'Dr. Amanda Foster', buyerContact: 'Mark Stevens', terms: 'Net 30 from execution date. Wire transfer to seller designated account.', notes: 'Executed on schedule. Funds transferred 2024-09-05.' },
  bos002: { ...MOCK_BILLS_OF_SALE[1], originalLienAmount: 15000, discountPercent: 20.0, sellerContact: 'Jane Miller', buyerContact: 'Mark Stevens', terms: 'Net 15 from execution. ACH transfer.', notes: 'Awaiting buyer signature.' },
};

export const MOCK_SERVICING: ServicingItem[] = [
  { id: 'sv001', taskNumber: 'SVC-2024-0001', taskType: 'Lien Verification', status: 'InProgress', priority: 'High', caseNumber: 'CASE-2024-0001', lienNumber: 'LN-2024-0001', assignedTo: 'Sarah Chen', description: 'Verify medical lien amounts against treatment records', dueDate: '2024-11-15', createdAtUtc: '2024-10-28T10:00:00Z', updatedAtUtc: '2024-11-01T14:00:00Z' },
  { id: 'sv002', taskNumber: 'SVC-2024-0002', taskType: 'Document Collection', status: 'Pending', priority: 'Normal', caseNumber: 'CASE-2024-0002', lienNumber: 'LN-2024-0003', assignedTo: 'Michael Park', description: 'Collect updated billing statements from Cortex TBI', dueDate: '2024-11-20', createdAtUtc: '2024-10-30T09:00:00Z', updatedAtUtc: '2024-10-30T09:00:00Z' },
  { id: 'sv003', taskNumber: 'SVC-2024-0003', taskType: 'Payment Processing', status: 'Completed', priority: 'Urgent', caseNumber: 'CASE-2024-0003', lienNumber: 'LN-2024-0004', assignedTo: 'Lisa Wang', description: 'Process payment for executed bill of sale BOS-2024-0001', dueDate: '2024-09-15', createdAtUtc: '2024-09-02T08:00:00Z', updatedAtUtc: '2024-09-10T16:00:00Z' },
  { id: 'sv004', taskNumber: 'SVC-2024-0004', taskType: 'Lien Negotiation', status: 'Escalated', priority: 'High', caseNumber: 'CASE-2024-0004', lienNumber: 'LN-2024-0005', assignedTo: 'Sarah Chen', description: 'Negotiate reduction on workers comp lien with UMC', dueDate: '2024-11-10', createdAtUtc: '2024-10-15T11:00:00Z', updatedAtUtc: '2024-11-02T10:00:00Z' },
  { id: 'sv005', taskNumber: 'SVC-2024-0005', taskType: 'Settlement Distribution', status: 'OnHold', priority: 'Normal', caseNumber: 'CASE-2024-0004', assignedTo: 'Lisa Wang', description: 'Prepare settlement distribution breakdown for Kim case', dueDate: '2024-11-30', createdAtUtc: '2024-10-20T13:00:00Z', updatedAtUtc: '2024-10-25T09:00:00Z' },
  { id: 'sv006', taskNumber: 'SVC-2024-0006', taskType: 'Document Collection', status: 'Pending', priority: 'Low', caseNumber: 'CASE-2024-0006', assignedTo: 'Michael Park', description: 'Request updated records from Spring Valley Hospital', dueDate: '2024-12-01', createdAtUtc: '2024-11-01T08:00:00Z', updatedAtUtc: '2024-11-01T08:00:00Z' },
];

export const MOCK_SERVICING_DETAILS: Record<string, ServicingDetail> = {
  sv001: {
    ...MOCK_SERVICING[0],
    notes: 'Initial verification shows discrepancy between billed and documented services. Need itemized statement.',
    linkedCaseId: 'c001', linkedLienId: 'l001',
    history: [
      { action: 'Task Created', timestamp: '2024-10-28T10:00:00Z', actor: 'System' },
      { action: 'Assigned to Sarah Chen', timestamp: '2024-10-28T10:05:00Z', actor: 'Auto-Assignment' },
      { action: 'Status changed to InProgress', timestamp: '2024-10-29T09:00:00Z', actor: 'Sarah Chen' },
      { action: 'Requested itemized billing statement', timestamp: '2024-10-30T14:00:00Z', actor: 'Sarah Chen', note: 'Emailed billing dept at Desert Springs Medical' },
      { action: 'Follow-up scheduled', timestamp: '2024-11-01T14:00:00Z', actor: 'Sarah Chen', note: 'No response yet. Will call on 11/04.' },
    ],
  },
  sv004: {
    ...MOCK_SERVICING[3],
    notes: 'UMC unwilling to negotiate below 90% of original amount. Escalated to senior management.',
    linkedCaseId: 'c004', linkedLienId: 'l005',
    history: [
      { action: 'Task Created', timestamp: '2024-10-15T11:00:00Z', actor: 'System' },
      { action: 'Initial contact with UMC billing', timestamp: '2024-10-18T10:00:00Z', actor: 'Sarah Chen' },
      { action: 'Negotiation offer rejected', timestamp: '2024-10-25T15:00:00Z', actor: 'Sarah Chen', note: 'UMC requires minimum 90% recovery' },
      { action: 'Escalated to management', timestamp: '2024-11-02T10:00:00Z', actor: 'Sarah Chen', note: 'Requesting VP-level intervention' },
    ],
  },
};

export const MOCK_CONTACTS: ContactSummary[] = [
  { id: 'ct001', contactType: 'LawFirm', name: 'John Martinez', organization: 'Cortex TBI of Nevada', email: 'jmartinez@cortextbi.com', phone: '(702) 555-1001', city: 'Las Vegas', state: 'NV', activeCases: 462, createdAtUtc: '2023-01-15T10:00:00Z' },
  { id: 'ct002', contactType: 'LawFirm', name: 'Emily Chen', organization: 'QA Lawfirm', email: 'echen@qalawfirm.com', phone: '(702) 555-1002', city: 'Henderson', state: 'NV', activeCases: 6, createdAtUtc: '2023-03-20T09:00:00Z' },
  { id: 'ct003', contactType: 'Provider', name: 'Dr. Amanda Foster', organization: 'Desert Springs Medical', email: 'afoster@desertsprings.com', phone: '(702) 555-2001', city: 'Las Vegas', state: 'NV', activeCases: 45, createdAtUtc: '2023-02-10T08:00:00Z' },
  { id: 'ct004', contactType: 'Provider', name: 'Dr. Kevin Park', organization: 'Valley Orthopedics', email: 'kpark@valleyortho.com', phone: '(702) 555-2002', city: 'Las Vegas', state: 'NV', activeCases: 28, createdAtUtc: '2023-04-05T11:00:00Z' },
  { id: 'ct005', contactType: 'LienHolder', name: 'Mark Stevens', organization: 'MedLien Capital', email: 'mstevens@medliencap.com', phone: '(212) 555-3001', city: 'New York', state: 'NY', activeCases: 89, createdAtUtc: '2023-01-20T10:00:00Z' },
  { id: 'ct006', contactType: 'LienHolder', name: 'Rachel Wong', organization: 'National Lien Buyers LLC', email: 'rwong@nlbuyers.com', phone: '(310) 555-3002', city: 'Los Angeles', state: 'CA', activeCases: 34, createdAtUtc: '2023-06-12T09:00:00Z' },
  { id: 'ct007', contactType: 'CaseManager', name: 'Sarah Chen', organization: 'LegalSynq', email: 'schen@legalsynq.com', phone: '(702) 555-4001', city: 'Las Vegas', state: 'NV', activeCases: 156, createdAtUtc: '2022-08-01T08:00:00Z' },
  { id: 'ct008', contactType: 'CaseManager', name: 'Michael Park', organization: 'LegalSynq', email: 'mpark@legalsynq.com', phone: '(702) 555-4002', city: 'Las Vegas', state: 'NV', activeCases: 98, createdAtUtc: '2022-10-15T10:00:00Z' },
  { id: 'ct009', contactType: 'InternalUser', name: 'Lisa Wang', organization: 'LegalSynq', email: 'lwang@legalsynq.com', phone: '(702) 555-4003', city: 'Las Vegas', state: 'NV', activeCases: 72, createdAtUtc: '2023-01-05T07:30:00Z' },
  { id: 'ct010', contactType: 'Provider', name: 'Dr. Robert Adams', organization: 'Sunrise Hospital', email: 'radams@sunrisehospital.com', phone: '(702) 555-2003', city: 'Las Vegas', state: 'NV', activeCases: 15, createdAtUtc: '2023-05-18T14:00:00Z' },
];

export const MOCK_CONTACT_DETAILS: Record<string, ContactDetail> = {
  ct001: { ...MOCK_CONTACTS[0], title: 'Managing Partner', address: '3960 Howard Hughes Pkwy, Suite 500', zipCode: '89169', website: 'https://cortextbi.com', notes: 'Primary law firm partner. Volume discount agreement in place.' },
  ct003: { ...MOCK_CONTACTS[2], title: 'Director of Medical Records', address: '2075 E Flamingo Rd', zipCode: '89119', website: 'https://desertspringsmed.com', notes: 'Responsive to lien inquiries. Typical turnaround 3-5 business days.' },
  ct005: { ...MOCK_CONTACTS[4], title: 'VP of Acquisitions', address: '350 Fifth Avenue, Suite 5100', zipCode: '10118', website: 'https://medliencapital.com', notes: 'Preferred buyer. Fast close capability. Minimum lien value $10,000.' },
};

export const MOCK_DOCUMENTS: DocumentSummary[] = [
  { id: 'doc001', documentNumber: 'DOC-2024-0001', fileName: 'gonzalez-mri-report.pdf', category: 'MedicalRecord', status: 'Completed', linkedEntity: 'Case', linkedEntityId: 'CASE-2024-0001', uploadedBy: 'Sarah Chen', fileSize: '2.4 MB', createdAtUtc: '2024-03-28T10:00:00Z' },
  { id: 'doc002', documentNumber: 'DOC-2024-0002', fileName: 'thompson-billing-statement.pdf', category: 'Financial', status: 'Completed', linkedEntity: 'Lien', linkedEntityId: 'LN-2024-0003', uploadedBy: 'Michael Park', fileSize: '856 KB', createdAtUtc: '2024-02-15T09:00:00Z' },
  { id: 'doc003', documentNumber: 'DOC-2024-0003', fileName: 'davis-demand-letter.docx', category: 'LegalFiling', status: 'Processing', linkedEntity: 'Case', linkedEntityId: 'CASE-2024-0003', uploadedBy: 'Sarah Chen', fileSize: '1.1 MB', createdAtUtc: '2024-10-28T14:00:00Z' },
  { id: 'doc004', documentNumber: 'DOC-2024-0004', fileName: 'bos-2024-0001-executed.pdf', category: 'Contract', status: 'Completed', linkedEntity: 'Bill of Sale', linkedEntityId: 'BOS-2024-0001', uploadedBy: 'Lisa Wang', fileSize: '3.2 MB', createdAtUtc: '2024-09-01T16:00:00Z' },
  { id: 'doc005', documentNumber: 'DOC-2024-0005', fileName: 'kim-settlement-agreement.pdf', category: 'LegalFiling', status: 'Completed', linkedEntity: 'Case', linkedEntityId: 'CASE-2024-0004', uploadedBy: 'Lisa Wang', fileSize: '5.8 MB', createdAtUtc: '2024-09-15T10:00:00Z' },
  { id: 'doc006', documentNumber: 'DOC-2024-0006', fileName: 'hernandez-medical-records.pdf', category: 'MedicalRecord', status: 'Pending', linkedEntity: 'Case', linkedEntityId: 'CASE-2024-0005', uploadedBy: 'Michael Park', fileSize: '4.1 MB', createdAtUtc: '2024-10-30T11:00:00Z' },
  { id: 'doc007', documentNumber: 'DOC-2024-0007', fileName: 'lien-purchase-agreement-template.docx', category: 'Contract', status: 'Archived', linkedEntity: 'System', linkedEntityId: 'TEMPLATE', uploadedBy: 'System', fileSize: '245 KB', createdAtUtc: '2023-06-01T08:00:00Z' },
  { id: 'doc008', documentNumber: 'DOC-2024-0008', fileName: 'batch-import-errors-oct24.csv', category: 'Other', status: 'Failed', linkedEntity: 'System', linkedEntityId: 'BATCH', uploadedBy: 'Michael Park', fileSize: '128 KB', createdAtUtc: '2024-10-25T15:00:00Z' },
];

export const MOCK_DOCUMENT_DETAILS: Record<string, DocumentDetail> = {
  doc001: { ...MOCK_DOCUMENTS[0], description: 'MRI report showing traumatic brain injury findings for Gonzalez case.', mimeType: 'application/pdf', version: 1, tags: ['medical', 'imaging', 'TBI'], processingNotes: 'OCR completed. Text extraction successful.' },
  doc003: { ...MOCK_DOCUMENTS[2], description: 'Demand letter for Davis bilateral hip case. Includes itemized damages.', mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', version: 2, tags: ['legal', 'demand', 'negotiation'], processingNotes: 'Converting to PDF for final review.' },
};

export const MOCK_USERS: LienUser[] = [
  { id: 'u001', name: 'Sarah Chen', email: 'schen@legalsynq.com', role: 'Case Manager', status: 'Active', department: 'Operations', lastLoginAtUtc: '2024-11-02T08:15:00Z', createdAtUtc: '2022-08-01T08:00:00Z' },
  { id: 'u002', name: 'Michael Park', email: 'mpark@legalsynq.com', role: 'Case Manager', status: 'Active', department: 'Operations', lastLoginAtUtc: '2024-11-01T16:30:00Z', createdAtUtc: '2022-10-15T10:00:00Z' },
  { id: 'u003', name: 'Lisa Wang', email: 'lwang@legalsynq.com', role: 'Senior Case Manager', status: 'Active', department: 'Operations', lastLoginAtUtc: '2024-11-02T09:00:00Z', createdAtUtc: '2023-01-05T07:30:00Z' },
  { id: 'u004', name: 'David Torres', email: 'dtorres@legalsynq.com', role: 'Administrator', status: 'Active', department: 'IT', lastLoginAtUtc: '2024-11-02T07:45:00Z', createdAtUtc: '2022-06-01T10:00:00Z' },
  { id: 'u005', name: 'Jennifer Adams', email: 'jadams@legalsynq.com', role: 'Analyst', status: 'Active', department: 'Finance', lastLoginAtUtc: '2024-10-30T14:00:00Z', createdAtUtc: '2023-03-15T09:00:00Z' },
  { id: 'u006', name: 'Robert Lee', email: 'rlee@legalsynq.com', role: 'Case Manager', status: 'Inactive', department: 'Operations', lastLoginAtUtc: '2024-08-15T11:00:00Z', createdAtUtc: '2023-02-20T08:00:00Z' },
  { id: 'u007', name: 'Amanda Foster', email: 'afoster@legalsynq.com', role: 'Viewer', status: 'Invited', department: 'External', createdAtUtc: '2024-10-28T14:00:00Z' },
  { id: 'u008', name: 'Kevin Nguyen', email: 'knguyen@legalsynq.com', role: 'Case Manager', status: 'Locked', department: 'Operations', lastLoginAtUtc: '2024-09-20T10:00:00Z', createdAtUtc: '2023-07-10T11:00:00Z' },
];

export const MOCK_USER_DETAILS: Record<string, LienUserDetail> = {
  u001: {
    ...MOCK_USERS[0],
    phone: '(702) 555-4001', title: 'Senior Case Manager',
    permissions: ['cases.view', 'cases.edit', 'liens.view', 'liens.edit', 'documents.view', 'documents.upload', 'contacts.view', 'contacts.edit', 'servicing.view', 'servicing.edit', 'reports.view'],
    activityLog: [
      { action: 'Logged in', timestamp: '2024-11-02T08:15:00Z' },
      { action: 'Updated Case CASE-2024-0001', timestamp: '2024-11-01T14:30:00Z' },
      { action: 'Uploaded document DOC-2024-0003', timestamp: '2024-10-28T14:00:00Z' },
      { action: 'Created servicing task SVC-2024-0001', timestamp: '2024-10-28T10:00:00Z' },
    ],
  },
  u004: {
    ...MOCK_USERS[3],
    phone: '(702) 555-5001', title: 'System Administrator',
    permissions: ['admin.full', 'cases.view', 'cases.edit', 'cases.delete', 'liens.view', 'liens.edit', 'liens.delete', 'documents.view', 'documents.upload', 'documents.delete', 'contacts.view', 'contacts.edit', 'contacts.delete', 'servicing.view', 'servicing.edit', 'reports.view', 'reports.export', 'users.manage', 'settings.manage'],
    activityLog: [
      { action: 'Logged in', timestamp: '2024-11-02T07:45:00Z' },
      { action: 'Updated user permissions for u006', timestamp: '2024-11-01T16:00:00Z' },
      { action: 'Exported monthly report', timestamp: '2024-10-31T10:00:00Z' },
    ],
  },
};

export const MOCK_RECENT_ACTIVITY = [
  { id: 'a1', type: 'case_update', description: 'Case CASE-2024-0003 moved to In Negotiation', actor: 'Sarah Chen', timestamp: '2024-11-02T16:45:00Z', icon: 'ri-folder-open-line', color: 'text-blue-600' },
  { id: 'a2', type: 'lien_offer', description: 'New offer received on LN-2024-0003 from MedLien Capital', actor: 'System', timestamp: '2024-10-30T09:00:00Z', icon: 'ri-money-dollar-circle-line', color: 'text-green-600' },
  { id: 'a3', type: 'document', description: 'Demand letter uploaded for Davis case', actor: 'Sarah Chen', timestamp: '2024-10-28T14:00:00Z', icon: 'ri-file-copy-2-line', color: 'text-purple-600' },
  { id: 'a4', type: 'servicing', description: 'Payment processed for BOS-2024-0001', actor: 'Lisa Wang', timestamp: '2024-09-10T16:00:00Z', icon: 'ri-tools-line', color: 'text-amber-600' },
  { id: 'a5', type: 'settlement', description: 'Kim case settled for $385,000', actor: 'Lisa Wang', timestamp: '2024-09-15T10:00:00Z', icon: 'ri-scales-3-line', color: 'text-emerald-600' },
  { id: 'a6', type: 'bos_executed', description: 'Bill of Sale BOS-2024-0005 executed', actor: 'System', timestamp: '2024-10-28T11:00:00Z', icon: 'ri-receipt-line', color: 'text-indigo-600' },
];

export const MOCK_DASHBOARD_TASKS = [
  { id: 'dt1', title: 'Verify lien amounts for Gonzalez case', priority: 'High', dueDate: '2024-11-15', assignedTo: 'Sarah Chen', status: 'InProgress', caseRef: 'CASE-2024-0001' },
  { id: 'dt2', title: 'Collect billing from Cortex TBI', priority: 'Normal', dueDate: '2024-11-20', assignedTo: 'Michael Park', status: 'Pending', caseRef: 'CASE-2024-0002' },
  { id: 'dt3', title: 'Negotiate UMC workers comp lien', priority: 'High', dueDate: '2024-11-10', assignedTo: 'Sarah Chen', status: 'Escalated', caseRef: 'CASE-2024-0004' },
  { id: 'dt4', title: 'Prepare settlement distribution for Kim', priority: 'Normal', dueDate: '2024-11-30', assignedTo: 'Lisa Wang', status: 'OnHold', caseRef: 'CASE-2024-0004' },
  { id: 'dt5', title: 'Review new offer on LN-2024-0003', priority: 'Urgent', dueDate: '2024-11-05', assignedTo: 'Sarah Chen', status: 'Pending', caseRef: 'CASE-2024-0002' },
];

export function formatCurrency(amount?: number): string {
  if (amount == null) return '\u2014';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(amount);
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
}

export function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  if (days < 30) return `${days}d ago`;
  return formatDate(iso);
}
