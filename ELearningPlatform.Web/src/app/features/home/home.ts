import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

interface Feature {
  icon: string;
  title: string;
  text: string;
}

// Only what the backend really does; no payments, certificates or progress tracking.
const FEATURES: Feature[] = [
  { icon: 'library_books', title: 'Course catalog', text: '10 specializations, fuzzy search by name, pagination, top-rated course.' },
  { icon: 'play_circle', title: 'Video lessons', text: 'Streaming with seeking, likes/dislikes, trending videos, video search.' },
  { icon: 'quiz', title: 'Exams', text: 'Multiple-choice and True/False tests, a scheduled window, automatic grading and results.' },
  { icon: 'star', title: 'Ratings and comments', text: 'Rate courses 1-5, discuss under courses and videos.' },
  { icon: 'security', title: 'Security', text: 'JWT + refresh tokens, 2FA by Email/SMS, Google sign-in, password reset.' },
  { icon: 'admin_panel_settings', title: 'Three roles', text: 'Student, teacher and admin, with user management and email/SMS broadcasts.' },
];

@Component({
  selector: 'app-home',
  imports: [RouterLink, MatButtonModule, MatCardModule, MatIconModule],
  template: `
    <section class="hero">
      <h1>Learn, teach, manage - all in one place</h1>
      <p>An e-learning platform with courses, videos, exams and role-based access.</p>
      <a mat-flat-button routerLink="/courses">Browse the catalog</a>
    </section>
    <div class="grid">
      @for (f of features; track f.title) {
        <mat-card appearance="outlined">
          <mat-card-header>
            <mat-icon mat-card-avatar>{{ f.icon }}</mat-icon>
            <mat-card-title>{{ f.title }}</mat-card-title>
          </mat-card-header>
          <mat-card-content>{{ f.text }}</mat-card-content>
        </mat-card>
      }
    </div>
  `,
  styles: `
    .hero { text-align: center; padding: 48px 0; }
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px; }
    mat-card-content { padding-top: 12px; }
  `,
})
export class Home {
  protected readonly features = FEATURES;
}
