import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { safeReturnUrl } from '../core/auth/return-url';
import { SessionService } from '../core/auth/session.service';

const INVALID_CREDENTIALS =
  'E-mail ou senha inválidos. Após 5 tentativas erradas, o acesso é bloqueado por 15 minutos.';
const TOO_MANY_ATTEMPTS = 'Há tentativas demais. Aguarde um pouco e tente de novo.';
const UNAVAILABLE = 'Não foi possível entrar agora. Tente novamente em instantes.';

/** Tela de login do gestor (protótipo T01). Não existe cadastro: a conta nasce da configuração (DT-29). */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
})
export class Login {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', Validators.required],
    password: ['', Validators.required],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Mostra o aviso de campo obrigatório depois que o campo foi tocado ou o envio foi tentado. */
  protected showRequired(field: 'email' | 'password'): boolean {
    const control = this.form.controls[field];
    return control.invalid && control.touched;
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.error.set(null);
    this.submitting.set(true);

    this.session.login(email.trim(), password).subscribe({
      // O destino vem da URL, que qualquer um pode forjar: só entra um caminho interno.
      next: () =>
        void this.router.navigateByUrl(
          safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')),
        ),
      error: (failure: unknown) => {
        this.submitting.set(false);
        this.error.set(messageFor(failure));
        // Uma senha errada não fica parada na tela.
        this.form.controls.password.reset('');
      },
    });
  }
}

function messageFor(failure: unknown): string {
  if (failure instanceof HttpErrorResponse) {
    if (failure.status === 401) {
      return INVALID_CREDENTIALS;
    }

    if (failure.status === 429) {
      return TOO_MANY_ATTEMPTS;
    }
  }

  return UNAVAILABLE;
}
