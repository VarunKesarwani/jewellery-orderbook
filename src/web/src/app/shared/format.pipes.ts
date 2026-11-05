import { Pipe, PipeTransform } from '@angular/core';

/**
 * Money is stored and transported as integer paise. It is only ever turned into a
 * display string here, so no floating point arithmetic ever touches a total.
 */
@Pipe({ name: 'inr' })
export class InrPipe implements PipeTransform {
  private static readonly formatter = new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  });

  transform(paise: number | null | undefined): string {
    if (paise === null || paise === undefined) {
      return '—';
    }
    return InrPipe.formatter.format(paise / 100);
  }
}

/** Grams, three decimals, from the milligram integer the API stores. */
@Pipe({ name: 'grams' })
export class GramsPipe implements PipeTransform {
  transform(milligrams: number | null | undefined): string {
    if (milligrams === null || milligrams === undefined) {
      return '—';
    }
    return `${(milligrams / 1000).toFixed(3)} g`;
  }
}
