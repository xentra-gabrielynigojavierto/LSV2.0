# Liens Service API Documentation

## Table of Contents

- [Overview](#overview)
- [Authentication & Authorization](#authentication--authorization)
- [Permissions Reference](#permissions-reference)
- [Common Models](#common-models)
- [Error Responses](#error-responses)
- [Liens](#liens-endpoints)
- [Cases](#cases-endpoints)
- [Bills of Sale](#bills-of-sale-endpoints)
- [Lien Offers](#lien-offers-endpoints)
- [Contacts](#contacts-endpoints)
- [Servicing](#servicing-endpoints)

---

## Overview

Base URL prefix: `/api/liens`

All endpoints in the Liens service are JSON-based (except document download endpoints which return files). Request and response bodies use `application/json` content type unless otherwise noted.

---

## Authentication & Authorization

Every endpoint requires:

1. **Authenticated user** — the caller must be an authenticated user (policy: `AuthenticatedUser`).
2. **Product access** — the caller must have access to the `SYNQ_LIENS` product.
3. **Endpoint-specific permission** — each endpoint requires a specific permission as listed in the tables below.

Requests missing authentication receive a `401 Unauthorized` response.

---

## Permissions Reference

| Permission Code | Description |
|---|---|
| `SYNQ_LIENS.lien:read` | Read liens, bills of sale, and lien offers |
| `SYNQ_LIENS.lien:create` | Create new liens |
| `SYNQ_LIENS.lien:update` | Update existing liens; accept lien offers |
| `SYNQ_LIENS.lien:offer` | Create lien offers |
| `SYNQ_LIENS.lien:service` | Manage bills of sale lifecycle (submit/execute/cancel); manage contacts and servicing items |
| `SYNQ_LIENS.case:read` | Read cases |
| `SYNQ_LIENS.case:create` | Create new cases |
| `SYNQ_LIENS.case:update` | Update existing cases |

The following permissions are defined in the system but not currently used by any API endpoint:

| Permission Code | Description |
|---|---|
| `SYNQ_LIENS.lien:read:own` | Read own liens |
| `SYNQ_LIENS.lien:browse` | Browse liens |
| `SYNQ_LIENS.lien:purchase` | Purchase liens |
| `SYNQ_LIENS.lien:read:held` | Read held liens |
| `SYNQ_LIENS.lien:settle` | Settle liens |

---

## Common Models

### PaginatedResult\<T\>

All list/search endpoints return results wrapped in this paginated envelope.

| Field | Type | Description |
|---|---|---|
| `items` | `T[]` | Array of result items for the current page |
| `page` | `integer` | Current page number |
| `pageSize` | `integer` | Number of items per page |
| `totalCount` | `integer` | Total number of matching items across all pages |

---

## Error Responses

### 401 Unauthorized

Returned when the request lacks valid authentication credentials.

### 403 Forbidden

Returned when the user is authenticated but does not have the required product access (`SYNQ_LIENS`) or the endpoint-specific permission.

### 404 Not Found

Returned when a requested resource does not exist.

```json
{
  "error": {
    "code": "not_found",
    "message": "Resource description not found."
  }
}
```

### Common Status Codes

In addition to the endpoint-specific success and error codes documented below, **every** endpoint may return:

| Status | Condition |
|---|---|
| `401 Unauthorized` | Missing or invalid authentication |
| `403 Forbidden` | Authenticated but lacking required product access or permission |

**Per-endpoint status code summary:**

| Endpoint Type | Success | Possible Errors |
|---|---|---|
| List / Search (`GET` returning paginated results) | `200 OK` | `401`, `403` |
| Get by ID / Get by number (`GET` returning single item) | `200 OK` | `401`, `403`, `404` |
| Create (`POST`) | `201 Created` | `401`, `403` |
| Update / Action (`PUT` or `POST` on `{id}`) | `200 OK` | `401`, `403`, `404` |
| Document download (`GET` returning file) | `200 OK` | `401`, `403`, `404` |

---

## Liens Endpoints

Base path: `/api/liens/liens`

### GET `/api/liens/liens`

Search and list liens with optional filters.

**Permission:** `SYNQ_LIENS.lien:read`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `status` | `string` | No | `null` | Filter by lien status |
| `lienType` | `string` | No | `null` | Filter by lien type |
| `caseId` | `guid` | No | `null` | Filter by associated case ID |
| `facilityId` | `guid` | No | `null` | Filter by facility ID |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<LienResponse>
```

---

### GET `/api/liens/liens/{id}`

Get a lien by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Lien unique identifier |

**Response:** `200 OK` — `LienResponse`

**Error:** `404 Not Found` — if the lien does not exist.

---

### GET `/api/liens/liens/by-number/{lienNumber}`

Get a lien by its lien number.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `lienNumber` | `string` | Lien number |

**Response:** `200 OK` — `LienResponse`

**Error:** `404 Not Found` — if the lien does not exist.

---

### POST `/api/liens/liens`

Create a new lien.

**Permission:** `SYNQ_LIENS.lien:create`

**Request Body: `CreateLienRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `lienNumber` | `string` | Yes | No | Unique lien number |
| `externalReference` | `string` | No | Yes | External reference identifier |
| `lienType` | `string` | Yes | No | Type of lien |
| `caseId` | `guid` | No | Yes | Associated case ID |
| `facilityId` | `guid` | No | Yes | Associated facility ID |
| `originalAmount` | `decimal` | Yes | No | Original lien amount |
| `jurisdiction` | `string` | No | Yes | Jurisdiction |
| `isConfidential` | `boolean` | Yes | No | Whether the lien is confidential |
| `subjectFirstName` | `string` | No | Yes | Subject first name |
| `subjectLastName` | `string` | No | Yes | Subject last name |
| `incidentDate` | `date` | No | Yes | Date of incident (format: `YYYY-MM-DD`) |
| `description` | `string` | No | Yes | Description |

**Response:** `201 Created` — `LienResponse`

Returns the created lien with a `Location` header pointing to `/api/liens/liens/{id}`.

---

### PUT `/api/liens/liens/{id}`

Update an existing lien.

**Permission:** `SYNQ_LIENS.lien:update`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Lien unique identifier |

**Request Body: `UpdateLienRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `externalReference` | `string` | No | Yes | External reference identifier |
| `lienType` | `string` | Yes | No | Type of lien |
| `caseId` | `guid` | No | Yes | Associated case ID |
| `facilityId` | `guid` | No | Yes | Associated facility ID |
| `originalAmount` | `decimal` | Yes | No | Original lien amount |
| `jurisdiction` | `string` | No | Yes | Jurisdiction |
| `isConfidential` | `boolean` | No | Yes | Whether the lien is confidential |
| `subjectFirstName` | `string` | No | Yes | Subject first name |
| `subjectLastName` | `string` | No | Yes | Subject last name |
| `incidentDate` | `date` | No | Yes | Date of incident (format: `YYYY-MM-DD`) |
| `description` | `string` | No | Yes | Description |

**Response:** `200 OK` — `LienResponse`

**Error:** `404 Not Found` — if the lien does not exist.

---

### LienResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `lienNumber` | `string` | No | Lien number |
| `externalReference` | `string` | Yes | External reference |
| `lienType` | `string` | No | Type of lien |
| `status` | `string` | No | Current status |
| `caseId` | `guid` | Yes | Associated case ID |
| `facilityId` | `guid` | Yes | Associated facility ID |
| `originalAmount` | `decimal` | No | Original lien amount |
| `currentBalance` | `decimal` | Yes | Current balance |
| `offerPrice` | `decimal` | Yes | Current offer price |
| `purchasePrice` | `decimal` | Yes | Purchase price |
| `payoffAmount` | `decimal` | Yes | Payoff amount |
| `jurisdiction` | `string` | Yes | Jurisdiction |
| `isConfidential` | `boolean` | No | Confidentiality flag |
| `subjectFirstName` | `string` | Yes | Subject first name |
| `subjectLastName` | `string` | Yes | Subject last name |
| `subjectDisplayName` | `string` | Yes | Computed subject display name |
| `orgId` | `guid` | No | Owning organization ID |
| `sellingOrgId` | `guid` | Yes | Selling organization ID |
| `buyingOrgId` | `guid` | Yes | Buying organization ID |
| `holdingOrgId` | `guid` | Yes | Holding organization ID |
| `incidentDate` | `date` | Yes | Date of incident |
| `description` | `string` | Yes | Description |
| `openedAtUtc` | `datetime` | Yes | When the lien was opened |
| `closedAtUtc` | `datetime` | Yes | When the lien was closed |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Cases Endpoints

Base path: `/api/liens/cases`

### GET `/api/liens/cases`

Search and list cases with optional filters.

**Permission:** `SYNQ_LIENS.case:read`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `status` | `string` | No | `null` | Filter by case status |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<CaseResponse>
```

---

### GET `/api/liens/cases/{id}`

Get a case by its unique identifier.

**Permission:** `SYNQ_LIENS.case:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Case unique identifier |

**Response:** `200 OK` — `CaseResponse`

**Error:** `404 Not Found` — if the case does not exist.

---

### GET `/api/liens/cases/by-number/{caseNumber}`

Get a case by its case number.

**Permission:** `SYNQ_LIENS.case:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `caseNumber` | `string` | Case number |

**Response:** `200 OK` — `CaseResponse`

**Error:** `404 Not Found` — if the case does not exist.

---

### POST `/api/liens/cases`

Create a new case.

**Permission:** `SYNQ_LIENS.case:create`

**Request Body: `CreateCaseRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `caseNumber` | `string` | Yes | No | Unique case number |
| `clientFirstName` | `string` | Yes | No | Client first name |
| `clientLastName` | `string` | Yes | No | Client last name |
| `externalReference` | `string` | No | Yes | External reference identifier |
| `title` | `string` | No | Yes | Case title |
| `clientDob` | `date` | No | Yes | Client date of birth (format: `YYYY-MM-DD`) |
| `clientPhone` | `string` | No | Yes | Client phone number |
| `clientEmail` | `string` | No | Yes | Client email address |
| `clientAddress` | `string` | No | Yes | Client address |
| `dateOfIncident` | `date` | No | Yes | Date of incident (format: `YYYY-MM-DD`) |
| `insuranceCarrier` | `string` | No | Yes | Insurance carrier name |
| `policyNumber` | `string` | No | Yes | Insurance policy number |
| `claimNumber` | `string` | No | Yes | Insurance claim number |
| `description` | `string` | No | Yes | Case description |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `201 Created` — `CaseResponse`

Returns the created case with a `Location` header pointing to `/api/liens/cases/{id}`.

---

### PUT `/api/liens/cases/{id}`

Update an existing case.

**Permission:** `SYNQ_LIENS.case:update`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Case unique identifier |

**Request Body: `UpdateCaseRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `clientFirstName` | `string` | Yes | No | Client first name |
| `clientLastName` | `string` | Yes | No | Client last name |
| `externalReference` | `string` | No | Yes | External reference identifier |
| `title` | `string` | No | Yes | Case title |
| `clientDob` | `date` | No | Yes | Client date of birth (format: `YYYY-MM-DD`) |
| `clientPhone` | `string` | No | Yes | Client phone number |
| `clientEmail` | `string` | No | Yes | Client email address |
| `clientAddress` | `string` | No | Yes | Client address |
| `dateOfIncident` | `date` | No | Yes | Date of incident (format: `YYYY-MM-DD`) |
| `insuranceCarrier` | `string` | No | Yes | Insurance carrier name |
| `policyNumber` | `string` | No | Yes | Insurance policy number |
| `claimNumber` | `string` | No | Yes | Insurance claim number |
| `description` | `string` | No | Yes | Case description |
| `notes` | `string` | No | Yes | Additional notes |
| `status` | `string` | No | Yes | Case status |
| `demandAmount` | `decimal` | No | Yes | Demand amount |
| `settlementAmount` | `decimal` | No | Yes | Settlement amount |

**Response:** `200 OK` — `CaseResponse`

**Error:** `404 Not Found` — if the case does not exist.

---

### CaseResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `caseNumber` | `string` | No | Case number |
| `externalReference` | `string` | Yes | External reference |
| `title` | `string` | Yes | Case title |
| `clientFirstName` | `string` | No | Client first name |
| `clientLastName` | `string` | No | Client last name |
| `clientDisplayName` | `string` | No | Computed client display name |
| `status` | `string` | No | Current status |
| `dateOfIncident` | `date` | Yes | Date of incident |
| `clientDob` | `date` | Yes | Client date of birth |
| `clientPhone` | `string` | Yes | Client phone number |
| `clientEmail` | `string` | Yes | Client email address |
| `clientAddress` | `string` | Yes | Client address |
| `insuranceCarrier` | `string` | Yes | Insurance carrier name |
| `policyNumber` | `string` | Yes | Insurance policy number |
| `claimNumber` | `string` | Yes | Insurance claim number |
| `demandAmount` | `decimal` | Yes | Demand amount |
| `settlementAmount` | `decimal` | Yes | Settlement amount |
| `description` | `string` | Yes | Case description |
| `notes` | `string` | Yes | Additional notes |
| `openedAtUtc` | `datetime` | Yes | When the case was opened |
| `closedAtUtc` | `datetime` | Yes | When the case was closed |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Bills of Sale Endpoints

Base path: `/api/liens/bill-of-sales`

### GET `/api/liens/bill-of-sales`

Search and list bills of sale with optional filters.

**Permission:** `SYNQ_LIENS.lien:read`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `status` | `string` | No | `null` | Filter by bill of sale status |
| `lienId` | `guid` | No | `null` | Filter by associated lien ID |
| `sellerOrgId` | `guid` | No | `null` | Filter by seller organization ID |
| `buyerOrgId` | `guid` | No | `null` | Filter by buyer organization ID |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<BillOfSaleResponse>
```

---

### GET `/api/liens/bill-of-sales/{id}`

Get a bill of sale by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### GET `/api/liens/bill-of-sales/by-number/{billOfSaleNumber}`

Get a bill of sale by its bill of sale number.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `billOfSaleNumber` | `string` | Bill of sale number |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### GET `/api/liens/liens/{lienId}/bill-of-sales`

Get all bills of sale associated with a specific lien.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `lienId` | `guid` | Lien unique identifier |

**Response:** `200 OK` — `BillOfSaleResponse[]`

---

### GET `/api/liens/bill-of-sales/{id}/document`

Download the document file for a bill of sale by its ID.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Response:** `200 OK` — Binary file download with appropriate `Content-Type` and `Content-Disposition` headers.

---

### GET `/api/liens/bill-of-sales/by-number/{billOfSaleNumber}/document`

Download the document file for a bill of sale by its number.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `billOfSaleNumber` | `string` | Bill of sale number |

**Response:** `200 OK` — Binary file download with appropriate `Content-Type` and `Content-Disposition` headers.

---

### PUT `/api/liens/bill-of-sales/{id}/submit`

Submit a bill of sale for execution.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### PUT `/api/liens/bill-of-sales/{id}/execute`

Execute a bill of sale.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### PUT `/api/liens/bill-of-sales/{id}/cancel`

Cancel a bill of sale.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Bill of sale unique identifier |

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `reason` | `string` | No | `null` | Reason for cancellation |

**Response:** `200 OK` — `BillOfSaleResponse`

**Error:** `404 Not Found` — if the bill of sale does not exist.

---

### BillOfSaleResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `billOfSaleNumber` | `string` | No | Bill of sale number |
| `externalReference` | `string` | Yes | External reference |
| `status` | `string` | No | Current status |
| `lienId` | `guid` | No | Associated lien ID |
| `lienOfferId` | `guid` | No | Associated lien offer ID |
| `sellerOrgId` | `guid` | No | Seller organization ID |
| `buyerOrgId` | `guid` | No | Buyer organization ID |
| `purchaseAmount` | `decimal` | No | Purchase amount |
| `originalLienAmount` | `decimal` | No | Original lien amount |
| `discountPercent` | `decimal` | Yes | Discount percentage |
| `sellerContactName` | `string` | Yes | Seller contact name |
| `buyerContactName` | `string` | Yes | Buyer contact name |
| `terms` | `string` | Yes | Terms of sale |
| `notes` | `string` | Yes | Additional notes |
| `documentId` | `guid` | Yes | Associated document ID |
| `issuedAtUtc` | `datetime` | No | When the bill of sale was issued |
| `executedAtUtc` | `datetime` | Yes | When executed |
| `effectiveAtUtc` | `datetime` | Yes | When effective |
| `cancelledAtUtc` | `datetime` | Yes | When cancelled |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Lien Offers Endpoints

Base path: `/api/liens/offers`

### GET `/api/liens/offers`

Search and list lien offers with optional filters.

**Permission:** `SYNQ_LIENS.lien:read`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `lienId` | `guid` | No | `null` | Filter by lien ID |
| `status` | `string` | No | `null` | Filter by offer status |
| `buyerOrgId` | `guid` | No | `null` | Filter by buyer organization ID |
| `sellerOrgId` | `guid` | No | `null` | Filter by seller organization ID |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<LienOfferResponse>
```

---

### GET `/api/liens/offers/{id}`

Get a lien offer by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Lien offer unique identifier |

**Response:** `200 OK` — `LienOfferResponse`

**Error:** `404 Not Found` — if the lien offer does not exist.

---

### GET `/api/liens/liens/{lienId}/offers`

Get all offers associated with a specific lien.

**Permission:** `SYNQ_LIENS.lien:read`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `lienId` | `guid` | Lien unique identifier |

**Response:** `200 OK` — `LienOfferResponse[]`

---

### POST `/api/liens/offers`

Create a new lien offer.

**Permission:** `SYNQ_LIENS.lien:offer`

**Request Body: `CreateLienOfferRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `lienId` | `guid` | Yes | No | ID of the lien being offered on |
| `offerAmount` | `decimal` | Yes | No | Offer amount |
| `notes` | `string` | No | Yes | Additional notes |
| `expiresAtUtc` | `datetime` | No | Yes | Offer expiration date/time (UTC) |

**Response:** `201 Created` — `LienOfferResponse`

Returns the created offer with a `Location` header pointing to `/api/liens/offers/{id}`.

---

### POST `/api/liens/offers/{offerId}/accept`

Accept a lien offer.

**Permission:** `SYNQ_LIENS.lien:update`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `offerId` | `guid` | Lien offer unique identifier |

**Response:** `200 OK` — `SaleFinalizationResult`

**Error:** `404 Not Found` — if the lien offer does not exist.

---

### LienOfferResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `lienId` | `guid` | No | Associated lien ID |
| `offerAmount` | `decimal` | No | Offer amount |
| `status` | `string` | No | Current status |
| `buyerOrgId` | `guid` | No | Buyer organization ID |
| `sellerOrgId` | `guid` | No | Seller organization ID |
| `notes` | `string` | Yes | Offer notes |
| `responseNotes` | `string` | Yes | Response notes from the seller |
| `externalReference` | `string` | Yes | External reference |
| `offeredAtUtc` | `datetime` | No | When the offer was made |
| `expiresAtUtc` | `datetime` | Yes | When the offer expires |
| `respondedAtUtc` | `datetime` | Yes | When the offer was responded to |
| `withdrawnAtUtc` | `datetime` | Yes | When the offer was withdrawn |
| `isExpired` | `boolean` | No | Whether the offer has expired |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

### SaleFinalizationResult

Returned when a lien offer is accepted. Contains details about the finalized sale.

| Field | Type | Nullable | Description |
|---|---|---|---|
| `acceptedOfferId` | `guid` | No | ID of the accepted offer |
| `acceptedOfferStatus` | `string` | No | Final status of the accepted offer |
| `lienId` | `guid` | No | ID of the lien involved in the sale |
| `finalLienStatus` | `string` | No | Final status of the lien after sale |
| `billOfSaleId` | `guid` | No | ID of the generated bill of sale |
| `billOfSaleNumber` | `string` | No | Number of the generated bill of sale |
| `billOfSaleStatus` | `string` | No | Status of the generated bill of sale |
| `purchaseAmount` | `decimal` | No | Purchase amount |
| `originalLienAmount` | `decimal` | No | Original lien amount |
| `discountPercent` | `decimal` | Yes | Discount percentage |
| `documentId` | `guid` | Yes | Associated document ID |
| `competingOffersRejected` | `integer` | No | Number of competing offers that were rejected |
| `finalizedAtUtc` | `datetime` | No | When the sale was finalized |

---

## Contacts Endpoints

Base path: `/api/liens/contacts`

### GET `/api/liens/contacts`

Search and list contacts with optional filters.

**Permission:** `SYNQ_LIENS.lien:service`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `contactType` | `string` | No | `null` | Filter by contact type |
| `isActive` | `boolean` | No | `null` | Filter by active status |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<ContactResponse>
```

---

### GET `/api/liens/contacts/{id}`

Get a contact by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Contact unique identifier |

**Response:** `200 OK` — `ContactResponse`

**Error:** `404 Not Found` — if the contact does not exist.

---

### POST `/api/liens/contacts`

Create a new contact.

**Permission:** `SYNQ_LIENS.lien:service`

**Request Body: `CreateContactRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `contactType` | `string` | Yes | No | Type of contact |
| `firstName` | `string` | Yes | No | First name |
| `lastName` | `string` | Yes | No | Last name |
| `title` | `string` | No | Yes | Job title |
| `organization` | `string` | No | Yes | Organization name |
| `email` | `string` | No | Yes | Email address |
| `phone` | `string` | No | Yes | Phone number |
| `fax` | `string` | No | Yes | Fax number |
| `website` | `string` | No | Yes | Website URL |
| `addressLine1` | `string` | No | Yes | Street address |
| `city` | `string` | No | Yes | City |
| `state` | `string` | No | Yes | State |
| `postalCode` | `string` | No | Yes | Postal code |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `201 Created` — `ContactResponse`

Returns the created contact with a `Location` header pointing to `/api/liens/contacts/{id}`.

---

### PUT `/api/liens/contacts/{id}`

Update an existing contact.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Contact unique identifier |

**Request Body: `UpdateContactRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `contactType` | `string` | Yes | No | Type of contact |
| `firstName` | `string` | Yes | No | First name |
| `lastName` | `string` | Yes | No | Last name |
| `title` | `string` | No | Yes | Job title |
| `organization` | `string` | No | Yes | Organization name |
| `email` | `string` | No | Yes | Email address |
| `phone` | `string` | No | Yes | Phone number |
| `fax` | `string` | No | Yes | Fax number |
| `website` | `string` | No | Yes | Website URL |
| `addressLine1` | `string` | No | Yes | Street address |
| `city` | `string` | No | Yes | City |
| `state` | `string` | No | Yes | State |
| `postalCode` | `string` | No | Yes | Postal code |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `200 OK` — `ContactResponse`

**Error:** `404 Not Found` — if the contact does not exist.

---

### PUT `/api/liens/contacts/{id}/deactivate`

Deactivate a contact.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Contact unique identifier |

**Response:** `200 OK` — `ContactResponse`

**Error:** `404 Not Found` — if the contact does not exist.

---

### PUT `/api/liens/contacts/{id}/reactivate`

Reactivate a previously deactivated contact.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Contact unique identifier |

**Response:** `200 OK` — `ContactResponse`

**Error:** `404 Not Found` — if the contact does not exist.

---

### ContactResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `contactType` | `string` | No | Type of contact |
| `firstName` | `string` | No | First name |
| `lastName` | `string` | No | Last name |
| `displayName` | `string` | No | Computed display name |
| `title` | `string` | Yes | Job title |
| `organization` | `string` | Yes | Organization name |
| `email` | `string` | Yes | Email address |
| `phone` | `string` | Yes | Phone number |
| `fax` | `string` | Yes | Fax number |
| `website` | `string` | Yes | Website URL |
| `addressLine1` | `string` | Yes | Street address |
| `city` | `string` | Yes | City |
| `state` | `string` | Yes | State |
| `postalCode` | `string` | Yes | Postal code |
| `notes` | `string` | Yes | Additional notes |
| `isActive` | `boolean` | No | Whether the contact is active |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |

---

## Servicing Endpoints

Base path: `/api/liens/servicing`

### GET `/api/liens/servicing`

Search and list servicing items with optional filters.

**Permission:** `SYNQ_LIENS.lien:service`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `search` | `string` | No | `null` | Free-text search filter |
| `status` | `string` | No | `null` | Filter by status |
| `priority` | `string` | No | `null` | Filter by priority |
| `assignedTo` | `string` | No | `null` | Filter by assignee |
| `caseId` | `guid` | No | `null` | Filter by associated case ID |
| `lienId` | `guid` | No | `null` | Filter by associated lien ID |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `20` | Items per page |

**Response:** `200 OK`

```json
PaginatedResult<ServicingItemResponse>
```

---

### GET `/api/liens/servicing/{id}`

Get a servicing item by its unique identifier.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Servicing item unique identifier |

**Response:** `200 OK` — `ServicingItemResponse`

**Error:** `404 Not Found` — if the servicing item does not exist.

---

### POST `/api/liens/servicing`

Create a new servicing item.

**Permission:** `SYNQ_LIENS.lien:service`

**Request Body: `CreateServicingItemRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `taskNumber` | `string` | Yes | No | Unique task number |
| `taskType` | `string` | Yes | No | Type of task |
| `description` | `string` | Yes | No | Task description |
| `assignedTo` | `string` | Yes | No | Name of assignee |
| `assignedToUserId` | `guid` | No | Yes | User ID of assignee |
| `priority` | `string` | No | Yes | Priority level |
| `caseId` | `guid` | No | Yes | Associated case ID |
| `lienId` | `guid` | No | Yes | Associated lien ID |
| `dueDate` | `date` | No | Yes | Due date (format: `YYYY-MM-DD`) |
| `notes` | `string` | No | Yes | Additional notes |

**Response:** `201 Created` — `ServicingItemResponse`

Returns the created servicing item with a `Location` header pointing to `/api/liens/servicing/{id}`.

---

### PUT `/api/liens/servicing/{id}`

Update an existing servicing item.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Servicing item unique identifier |

**Request Body: `UpdateServicingItemRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `taskType` | `string` | Yes | No | Type of task |
| `description` | `string` | Yes | No | Task description |
| `assignedTo` | `string` | Yes | No | Name of assignee |
| `assignedToUserId` | `guid` | No | Yes | User ID of assignee |
| `priority` | `string` | No | Yes | Priority level |
| `status` | `string` | No | Yes | Status |
| `caseId` | `guid` | No | Yes | Associated case ID |
| `lienId` | `guid` | No | Yes | Associated lien ID |
| `dueDate` | `date` | No | Yes | Due date (format: `YYYY-MM-DD`) |
| `notes` | `string` | No | Yes | Additional notes |
| `resolution` | `string` | No | Yes | Resolution notes |

**Response:** `200 OK` — `ServicingItemResponse`

**Error:** `404 Not Found` — if the servicing item does not exist.

---

### PUT `/api/liens/servicing/{id}/status`

Update the status of a servicing item.

**Permission:** `SYNQ_LIENS.lien:service`

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Servicing item unique identifier |

**Request Body: `UpdateStatusRequest`**

| Field | Type | Required | Nullable | Description |
|---|---|---|---|---|
| `status` | `string` | Yes | No | New status value |
| `resolution` | `string` | No | Yes | Resolution notes |

**Response:** `200 OK` — `ServicingItemResponse`

**Error:** `404 Not Found` — if the servicing item does not exist.

---

### ServicingItemResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `taskNumber` | `string` | No | Task number |
| `taskType` | `string` | No | Type of task |
| `description` | `string` | No | Task description |
| `status` | `string` | No | Current status |
| `priority` | `string` | No | Priority level |
| `assignedTo` | `string` | No | Name of assignee |
| `assignedToUserId` | `guid` | Yes | User ID of assignee |
| `caseId` | `guid` | Yes | Associated case ID |
| `lienId` | `guid` | Yes | Associated lien ID |
| `dueDate` | `date` | Yes | Due date |
| `notes` | `string` | Yes | Additional notes |
| `resolution` | `string` | Yes | Resolution notes |
| `startedAtUtc` | `datetime` | Yes | When work was started |
| `completedAtUtc` | `datetime` | Yes | When work was completed |
| `escalatedAtUtc` | `datetime` | Yes | When item was escalated |
| `createdAtUtc` | `datetime` | No | Record creation timestamp |
| `updatedAtUtc` | `datetime` | No | Record last-updated timestamp |
