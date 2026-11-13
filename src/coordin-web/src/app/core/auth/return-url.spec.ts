import { safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it.each(['/cronograma', '/times/3', '/cronograma?mes=11', '/relatorio#topo'])(
    'keeps the internal path %s',
    (path) => {
      expect(safeReturnUrl(path)).toBe(path);
    },
  );

  it.each([
    ['//site-falso.com', 'a protocol-relative address'],
    ['//site-falso.com/login', 'a protocol-relative address with a path'],
    ['https://site-falso.com', 'an absolute URL'],
    ['http://site-falso.com', 'an absolute URL over http'],
    ['javascript:alert(1)', 'a javascript: URL'],
    ['/\\site-falso.com', 'a backslash, which browsers read as //'],
    ['\\\\site-falso.com', 'a UNC-like path'],
    ['/a\nb', 'a control character'],
    ['cronograma', 'a path without the leading slash'],
    ['/login', 'the login page itself (it would loop)'],
    ['/login?returnUrl=/x', 'the login page with parameters'],
    ['', 'an empty value'],
  ])('rejects %j (%s) and falls back to the home page', (candidate) => {
    expect(safeReturnUrl(candidate)).toBe('/');
  });

  it.each([undefined, null, 42, {}, ['/cronograma']])(
    'falls back to the home page for a non-string value (%j)',
    (candidate) => {
      expect(safeReturnUrl(candidate)).toBe('/');
    },
  );
});
