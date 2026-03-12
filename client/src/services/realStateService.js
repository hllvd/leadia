import api from './api';

const realStateService = {
  // ── Agencies ──
  getAgencies: () => api.get('/realstate/agencies'),
  getAgency: (id) => api.get(`/realstate/agencies/${id}`),
  createAgency: (data) => api.post('/realstate/agencies', data),
  updateAgency: (id, data) => api.put(`/realstate/agencies/${id}`, data),
  deleteAgency: (id) => api.delete(`/realstate/agencies/${id}`),

  // ── Assignments ──
  assignBroker: (agencyId, brokerId) => api.post(`/realstate/agencies/${agencyId}/assign/${brokerId}`),
  removeAssignment: (id) => api.delete(`/realstate/assignments/${id}`),

  // ── Broker Data ──
  getBrokerData: (brokerId) => api.get(`/realstate/broker-data/${brokerId}`),
  addBrokerData: (data) => api.post('/realstate/broker-data', data),
  updateBrokerData: (id, data) => api.put(`/realstate/broker-data/${id}`, data),
  deleteBrokerData: (id) => api.delete(`/realstate/broker-data/${id}`),
};

export default realStateService;
