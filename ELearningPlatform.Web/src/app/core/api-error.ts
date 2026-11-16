import { HttpErrorResponse } from '@angular/common/http';
import { ApiProblem } from './models/auth';

export function toProblem(err: unknown): ApiProblem {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return { status: 0, detail: 'Server is unavailable' };
    if (err.status === 429) return { status: 429, detail: 'Too many requests, try again in a minute' };
    const body = err.error;
    if (body && typeof body === 'object') return { ...body, status: err.status };
    return { status: err.status, detail: err.statusText };
  }
  return { status: 500, detail: 'Unknown error' };
}

const MESSAGES: Record<string, string> = {
  'Auth.InvalidCredentials': 'Invalid email or password',
  'Auth.LockedOut': 'Account is locked',
  'Auth.EmailNotConfirmed': 'Confirm your email - check your inbox',
  'Auth.InvalidRole': 'You do not have the selected role',
  'Auth.InvalidTwoFactorCode': 'Invalid code',
  'Auth.EmailAlreadyRegistered': 'This email is already registered',
  'Course.AlreadyRated': 'You have already rated this course',
  'Exam.NotStarted': 'The exam has not started yet',
  'Exam.Ended': 'The exam has ended',
  'Exam.AlreadySubmitted': 'You have already submitted this exam',
  'Question.LimitReached': 'Question limit reached',
  'Media.InvalidFile': 'Invalid file',
};

export function problemMessage(p: ApiProblem): string {
  if (p.code && MESSAGES[p.code]) return MESSAGES[p.code];
  if (p.errors) return Object.values(p.errors).flat().join('. ');
  return p.detail ?? p.title ?? 'Error';
}
