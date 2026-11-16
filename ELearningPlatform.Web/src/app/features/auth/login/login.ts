import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Observable } from 'rxjs';
import { problemMessage, toProblem } from '../../../core/api-error';
import { AuthService } from '../../../core/auth.service';
import { AuthDto, Role, TwoFactorMethod } from '../../../core/models/auth';

// Development-only seed accounts; the password is whatever Seed:AdminPassword / Seed:DevUsersPassword is set to.
const DEMO_PASSWORD = 'Demo#Pass123';

const DEMO_ACCOUNTS = [
  { label: 'Admin', email: 'admin@elearning.com', password: DEMO_PASSWORD, role: 'Admin' as Role },
  { label: 'Teacher', email: 'ivan.petrov@elearning.com', password: DEMO_PASSWORD, role: 'Teacher' as Role },
  { label: 'Student', email: 'anna.ivanova@elearning.com', password: DEMO_PASSWORD, role: 'Student' as Role },
];

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule, RouterLink, MatButtonModule, MatButtonToggleModule, MatCardModule,
    MatFormFieldModule, MatInputModule, MatProgressBarModule,
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly demo = DEMO_ACCOUNTS;
  protected readonly demoPassword = DEMO_PASSWORD;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Set once the API answers `Auth.TwoFactorRequired`; switches the card to the code step. */
  protected readonly twoFactorEmail = signal<string | null>(null);
  protected readonly method = signal<TwoFactorMethod>('Email');

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    role: ['Student' as Role, Validators.required],
  });
  protected readonly codeForm = this.fb.nonNullable.group({ code: ['', [Validators.required, Validators.minLength(4)]] });

  protected fillDemo(a: (typeof DEMO_ACCOUNTS)[number]) {
    this.form.patchValue({ email: a.email, password: a.password, role: a.role });
  }

  protected submit() {
    if (this.form.invalid) return;
    this.run(this.auth.login(this.form.getRawValue()), (code) => {
      if (code !== 'Auth.TwoFactorRequired') return false;
      this.twoFactorEmail.set(this.form.getRawValue().email);
      return true;
    });
  }

  protected switchMethod(method: TwoFactorMethod) {
    this.method.set(method);
    this.busy.set(true);
    this.auth.sendTwoFactorCode(this.twoFactorEmail()!, method).subscribe({
      next: () => this.busy.set(false),
      error: (e) => this.fail(e),
    });
  }

  protected verify() {
    if (this.codeForm.invalid) return;
    this.run(this.auth.verifyTwoFactor(this.twoFactorEmail()!, this.method(), this.codeForm.getRawValue().code));
  }

  private run(request: Observable<AuthDto>, handled?: (code?: string) => boolean) {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: () => this.router.navigateByUrl(this.route.snapshot.queryParamMap.get('returnUrl') ?? '/'),
      error: (e) => {
        if (handled?.(toProblem(e).code)) this.busy.set(false);
        else this.fail(e);
      },
    });
  }

  private fail(e: unknown) {
    this.busy.set(false);
    this.error.set(problemMessage(toProblem(e)));
  }
}
