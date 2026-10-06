export const passwordChecks = (value: string) => [value.length >= 12, /[A-Z]/.test(value), /[a-z]/.test(value), /\d/.test(value), /[^A-Za-z0-9]/.test(value), !/(password|qwerty|admin|welcome|123456)/i.test(value)];
export const passwordValid = (value: string) => passwordChecks(value).every(Boolean);
export const passwordStrength = (value: string) => { const count = passwordChecks(value).filter(Boolean).length; return count <= 2 ? 'Weak' : count <= 4 ? 'Fair' : count === 5 ? 'Strong' : 'Very Strong'; };
