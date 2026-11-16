import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from '../core/auth.service';
import { Role } from '../core/models/auth';

interface NavItem {
  label: string;
  link: string;
  roles?: Role[];
}

const NAV: NavItem[] = [
  { label: 'Catalog', link: '/courses' },
  { label: 'My exams', link: '/exams', roles: ['Student'] },
  { label: 'Teaching', link: '/teacher', roles: ['Teacher', 'Admin'] },
  { label: 'Administration', link: '/admin', roles: ['Admin'] },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule, MatIconModule],
  template: `
    <mat-toolbar>
      <a routerLink="/" class="brand"><mat-icon>school</mat-icon> E-Learning</a>
      <nav>
        @for (item of items(); track item.link) {
          <a mat-button [routerLink]="item.link" routerLinkActive="active">{{ item.label }}</a>
        }
      </nav>
      <span class="spacer"></span>
      @if (auth.isAuthenticated()) {
        <a mat-button routerLink="/profile"><mat-icon>person</mat-icon> {{ auth.role() }}</a>
        <button mat-stroked-button (click)="auth.logout().subscribe()">Log out</button>
      } @else {
        <a mat-button routerLink="/login">Log in</a>
        <a mat-flat-button routerLink="/register">Sign up</a>
      }
    </mat-toolbar>
    <main class="page"><router-outlet /></main>
  `,
  styles: `
    .brand { display: flex; align-items: center; gap: 8px; color: inherit; text-decoration: none; font-weight: 500; margin-right: 16px; }
    .spacer { flex: 1; }
    .active { background: var(--mat-sys-secondary-container); }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly items = computed(() => {
    const role = this.auth.role();
    return NAV.filter((i) => !i.roles || (role && i.roles.includes(role)));
  });
}
