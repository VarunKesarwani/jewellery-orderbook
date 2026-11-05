/** DTOs mirroring the API contracts. Keep in step with src/api/Contracts/Contracts.cs. */

export interface Customer {
  id: string;
  name: string;
  phone: string;
  email: string | null;
  gstin: string | null;
  isActive: boolean;
}

export interface CreateCustomer {
  name: string;
  phone?: string | null;
  email?: string | null;
  gstin?: string | null;
  billingAddress?: string | null;
  creditDays?: number;
}

export interface OrderLine {
  id: string;
  itemName: string;
  quantity: number;
  metalType: string;
  metalPurity: string | null;
  grossWeightMg: number;
  ratePerGramPaise: number;
  makingChargesPaise: number;
  lineTotalPaise: number;
}

export interface Order {
  id: string;
  number: string;
  customerId: string;
  orderType: string;
  status: string;
  orderDate: string;
  expectedDeliveryDate: string | null;
  totalPaise: number;
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
