import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  imports: [RouterLink],
  template: `<h2>Access denied</h2><p>Your role cannot open this page.</p><a routerLink="/">Back to home</a>`,
})
export class Forbidden {}
