import {
  ValidationError,
  AuthenticationError,
  ForbiddenError,
  NotFoundError,
  FileTooLargeError,
  UnsupportedFileTypeError,
} from '../../src/shared/errors';

describe('Error hierarchy', () => {
  it('ValidationError has status 400', () => {
    const e = new ValidationError('bad input');
    expect(e.statusCode).toBe(400);
    expect(e.code).toBe('VALIDATION_ERROR');
  });

  it('AuthenticationError has status 401', () => {
    expect(new AuthenticationError().statusCode).toBe(401);
  });

  it('ForbiddenError has status 403', () => {
    expect(new ForbiddenError().statusCode).toBe(403);
  });

  it('NotFoundError has status 404', () => {
    const e = new NotFoundError('Document', 'abc');
    expect(e.statusCode).toBe(404);
    expect(e.message).toContain('abc');
  });

  it('FileTooLargeError has status 413', () => {
    expect(new FileTooLargeError(50).statusCode).toBe(413);
  });

  it('UnsupportedFileTypeError has status 422', () => {
    expect(new UnsupportedFileTypeError('application/x-exe').statusCode).toBe(422);
  });
});
