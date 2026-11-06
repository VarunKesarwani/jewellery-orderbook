import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CreateCustomer, CurrentUser, Customer, Delivery, DeliveryRequest, Order, OrderRequest,
  Payment, PaymentRequest
} from './models';

/**
 * Thin typed wrapper over the Minimal API.
 * URLs are relative: the dev server proxies /api to the API (see proxy.conf.json),
 * and in production both sit behind the same reverse proxy.
 */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  me(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>('/api/me');
  }

  listCustomers(search?: string): Observable<Customer[]> {
    let params = new HttpParams();
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<Customer[]>('/api/customers', { params });
  }

  createCustomer(body: CreateCustomer): Observable<Customer> {
    return this.http.post<Customer>('/api/customers', body);
  }

  listOrders(status?: string): Observable<Order[]> {
    let params = new HttpParams();
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<Order[]>('/api/orders', { params });
  }

  getOrder(id: string): Observable<Order> {
    return this.http.get<Order>(`/api/orders/${id}`);
  }

  /** Prices an unsaved order. The server owns the arithmetic; this is the preview. */
  calculateOrder(body: OrderRequest): Observable<Order> {
    return this.http.post<Order>('/api/orders/calculate', body);
  }

  createOrder(body: OrderRequest): Observable<Order> {
    return this.http.post<Order>('/api/orders', body);
  }

  cancelOrder(id: string, reason: string): Observable<Order> {
    return this.http.post<Order>(`/api/orders/${id}/cancel`, { reason });
  }

  changeOrderStatus(id: string, status: string): Observable<Order> {
    return this.http.post<Order>(`/api/orders/${id}/status`, { status });
  }

  listDeliveries(orderId: string): Observable<Delivery[]> {
    const params = new HttpParams().set('orderId', orderId);
    return this.http.get<Delivery[]>('/api/deliveries', { params });
  }

  createDelivery(body: DeliveryRequest): Observable<Delivery> {
    return this.http.post<Delivery>('/api/deliveries', body);
  }

  listPayments(orderId: string): Observable<Payment[]> {
    const params = new HttpParams().set('orderId', orderId);
    return this.http.get<Payment[]>('/api/payments', { params });
  }

  createPayment(body: PaymentRequest): Observable<Payment> {
    return this.http.post<Payment>('/api/payments', body);
  }
}
