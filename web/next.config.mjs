// Proxy only in the loopback development preview. The API owns every auth and data check.
export default {
  async rewrites() {
    return process.env.NODE_ENV === 'development' && process.env.LOCAL_PREVIEW === '1'
      ? [{ source: '/api/v1/:path*', destination: 'http://127.0.0.1:5081/api/v1/:path*' }]
      : [];
  },
};
