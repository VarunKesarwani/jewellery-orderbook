/** DTOs mirroring the API contracts. Keep in step with src/api/Contracts/Contracts.cs. */

export interface Customer {
  id: string;
  name: string;
  phone: string;
  address: string;
  pan: string | null;
  email: string | null;
  gstin: string | null;
  creditDays: number;
  notes: string | null;
  isActive: boolean;
}

/** Name, phone and address are the only mandatory fields. */
export interface CreateCustomer {
  name: string;
  phone: string;
  address: string;
  pan?: string | null;
  email?: string | null;
  gstin?: string | null;
  creditDays?: number;
  notes?: string | null;
}

/** Whole percentages: 3, 5, 9, 12, 18 or 28. Null means the component is untaxed. */
export interface TaxConfig {
  gold: number | null;
  wastage: number | null;
  making: number | null;
  stone: number | null;
}

export interface Stone {
  id: string;
  category: string;
  name: string | null;
  shape: string | null;
  stoneCount: number;
  totalCaratsMilli: number;
  colourGrade: string | null;
  clarityGrade: string | null;
  certificationLab: string | null;
  certificateNumber: string | null;
  stoneValuePaise: number;
  settingType: string | null;
}

export interface OrderLine {
  id: string;
  itemName: string;
  itemDescription: string | null;
  quantity: number;
  metalType: string;
  metalPurity: string | null;
  grossWeightMg: number;
  netWeightMg: number;
  wastageWeightMg: number;
  ratePerGramPaise: number;
  rateDate: string;
  metalValuePaise: number;
  wastageChargePaise: number;
  makingChargesPaise: number;
  stoneValuePaise: number;
  otherChargesPaise: number;
  subtotalPaise: number;
  discountPaise: number;
  tax: TaxConfig;
  taxPaise: number;
  lineTotalPaise: number;
  deliveredPieces: number;
  stones: Stone[];
}

export interface Order {
  id: string;
  number: number;
  customerId: string;
  customerName: string;
  orderDate: string;
  expectedDeliveryDate: string | null;
  status: string;
  paymentState: string;
  notes: string | null;
  cancellationReason: string | null;
  subtotalPaise: number;
  taxPaise: number;
  roundOffPaise: number;
  finalAmountPaise: number;
  paidPaise: number;
  totalPieces: number;
  deliveredPieces: number;
  lines: OrderLine[];
}

export interface CurrentUser {
  subject: string;
  name: string | null;
  roles: string[];
  authenticated: boolean;
}

/** RFC 7807 problem details, with the stable code the API adds. */
export interface ApiProblem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
}

// ---------------------------------------------------------------- requests

/** Whole percentages: 3, 5, 9, 12, 18 or 28. Null means that component is untaxed. */
export interface TaxRequest {
  gold: number | null;
  wastage: number | null;
  making: number | null;
  stone: number | null;
}

export interface StoneRequest {
  category: string;
  name?: string | null;
  shape?: string | null;
  stoneCount: number;
  totalCaratsMilli: number;
  colourGrade?: string | null;
  clarityGrade?: string | null;
  certificationLab?: string | null;
  certificateNumber?: string | null;
  stoneValuePaise: number;
  settingType?: string | null;
}

export interface LineRequest {
  itemName: string;
  itemDescription?: string | null;
  quantity: number;
  metalType: string;
  metalPurity?: string | null;
  grossWeightMg: number;
  ratePerGramPaise: number;
  rateDate?: string | null;
  wastagePctBp: number;
  makingType: string;
  makingRate: number;
  otherChargesPaise: number;
  discountPaise: number;
  tax?: TaxRequest | null;
  stones?: StoneRequest[];
}

export interface OrderRequest {
  customerId: string;
  orderDate: string;
  expectedDeliveryDate?: string | null;
  notes?: string | null;
  tax?: TaxRequest | null;
  lines: LineRequest[];
}

export interface DeliveryLine {
  orderLineId: string;
  quantity: number;
}

export interface Delivery {
  id: string;
  number: number;
  orderId: string;
  deliveredOn: string;
  receivedBy: string | null;
  notes: string | null;
  totalPieces: number;
  lines: DeliveryLine[];
}

export interface DeliveryRequest {
  orderId: string;
  deliveredOn: string;
  receivedBy?: string | null;
  notes?: string | null;
  lines: DeliveryLine[];
}

export interface Payment {
  id: string;
  number: number;
  orderId: string;
  orderLineId: string | null;
  amountPaise: number;
  paidOn: string;
  method: string | null;
  reference: string | null;
  isRefund: boolean;
}

export interface PaymentRequest {
  orderId: string;
  orderLineId?: string | null;
  amountPaise: number;
  paidOn: string;
  method?: string | null;
  reference?: string | null;
}

// ---------------------------------------------------------------- reports

export interface DailyOrderRow {
  id: string;
  number: number;
  customer: string | null;
  pieces: number;
  subtotalPaise: number;
  taxPaise: number;
  finalAmountPaise: number;
}

export interface DailyOrdersReport {
  date: string;
  orderCount: number;
  pieces: number;
  valuePaise: number;
  taxPaise: number;
  rows: DailyOrderRow[];
}

export interface DailyDeliveryRow {
  id: string;
  orderId: string;
  number: number;
  orderNumber: number | null;
  customer: string | null;
  pieces: number;
  receivedBy: string | null;
}

export interface DailyDeliveriesReport {
  date: string;
  deliveryCount: number;
  pieces: number;
  rows: DailyDeliveryRow[];
}

export interface DailyPaymentRow {
  id: string;
  orderId: string;
  number: number;
  orderNumber: number | null;
  amountPaise: number;
  method: string | null;
  reference: string | null;
  isRefund: boolean;
}

export interface DailyPaymentsReport {
  date: string;
  paymentCount: number;
  collectedPaise: number;
  refundedPaise: number;
  netPaise: number;
  rows: DailyPaymentRow[];
}
