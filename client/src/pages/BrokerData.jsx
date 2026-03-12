import { useState, useEffect } from 'react';
import realStateService from '../services/realStateService';
import api from '../services/api';

export default function BrokerDataAdmin() {
  const [brokers, setBrokers] = useState([]);
  const [selectedBroker, setSelectedBroker] = useState('');
  const [brokerData, setBrokerData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [isAdding, setIsAdding] = useState(false);
  const [formData, setFormData] = useState({
    dataName: '',
    dataKey: 'phone',
    dataValue: '',
    isPreferred: false,
    description: ''
  });

  useEffect(() => {
    const fetchBrokers = async () => {
      try {
        const res = await api.get('/users');
        setBrokers(res.data.filter(u => u.role === 'Admin' || u.role === 'User'));
      } catch (err) {
        console.error('Erro ao buscar corretores', err);
      }
    };
    fetchBrokers();
  }, []);

  const fetchBrokerData = async (id) => {
    if (!id) return;
    setLoading(true);
    try {
      const res = await realStateService.getBrokerData(id);
      setBrokerData(res.data);
    } catch (err) {
      console.error('Erro ao buscar dados do corretor', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (selectedBroker) {
      fetchBrokerData(selectedBroker);
    } else {
      setBrokerData([]);
    }
  }, [selectedBroker]);

  const handleAdd = async (e) => {
    e.preventDefault();
    if (!selectedBroker) return;
    try {
      await realStateService.addBrokerData({
        ...formData,
        brokerId: selectedBroker
      });
      setIsAdding(false);
      setFormData({
        dataName: '',
        dataKey: 'phone',
        dataValue: '',
        isPreferred: false,
        description: ''
      });
      fetchBrokerData(selectedBroker);
    } catch (err) {
      alert('Erro ao adicionar dado');
    }
  };

  const handleDelete = async (id) => {
    if (!window.confirm('Excluir este dado?')) return;
    try {
      await realStateService.deleteBrokerData(id);
      fetchBrokerData(selectedBroker);
    } catch (err) {
      alert('Erro ao excluir dado');
    }
  };

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <h1>Dados dos Corretores</h1>
          <p className="page-subtitle">Gerencie informações flexíveis (telefones, e-mails, redes sociais) dos corretores.</p>
        </div>
      </header>

      <div className="card" style={{ marginBottom: '1.5rem' }}>
        <div className="form-group" style={{ maxWidth: '400px' }}>
          <label>Selecione um Corretor</label>
          <select 
            value={selectedBroker} 
            onChange={(e) => setSelectedBroker(e.target.value)}
            className="select"
          >
            <option value="">-- Selecione --</option>
            {brokers.map(b => (
              <option key={b.id} value={b.id}>{b.name} ({b.email})</option>
            ))}
          </select>
        </div>
      </div>

      {selectedBroker && (
        <>
          <div className="card">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
              <h3>Metadados do Corretor</h3>
              <button 
                className="btn btn-secondary" 
                onClick={() => setIsAdding(!isAdding)}
              >
                {isAdding ? 'Cancelar' : '+ Adicionar Dado'}
              </button>
            </div>

            {isAdding && (
              <form onSubmit={handleAdd} className="card-inner form-stack" style={{ marginBottom: '2rem', background: 'var(--bg-secondary)' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                  <div className="form-group">
                    <label>Nome do Campo (Visível)</label>
                    <input 
                      type="text" 
                      value={formData.dataName} 
                      onChange={(e) => setFormData({...formData, dataName: e.target.value})} 
                      required 
                      placeholder="Ex: Telefone Comercial"
                    />
                  </div>
                  <div className="form-group">
                    <label>Tipo (Chave)</label>
                    <select 
                      value={formData.dataKey} 
                      onChange={(e) => setFormData({...formData, dataKey: e.target.value})}
                      className="select"
                    >
                      <option value="phone">Telefone</option>
                      <option value="email">E-mail</option>
                      <option value="whatsapp">WhatsApp</option>
                      <option value="instagram">Instagram</option>
                      <option value="other">Outro</option>
                    </select>
                  </div>
                </div>
                <div className="form-group">
                  <label>Valor</label>
                  <input 
                    type="text" 
                    value={formData.dataValue} 
                    onChange={(e) => setFormData({...formData, dataValue: e.target.value})} 
                    required 
                    placeholder="Ex: +55 11 99999-9999"
                  />
                </div>
                <div className="form-group">
                  <label>Descrição</label>
                  <input 
                    type="text" 
                    value={formData.description} 
                    onChange={(e) => setFormData({...formData, description: e.target.value})} 
                    placeholder="Opcional"
                  />
                </div>
                <div className="form-group-row">
                  <input 
                    type="checkbox" 
                    id="isPreferred"
                    checked={formData.isPreferred} 
                    onChange={(e) => setFormData({...formData, isPreferred: e.target.checked})} 
                  />
                  <label htmlFor="isPreferred">Marcar como preferencial</label>
                </div>
                <button type="submit" className="btn btn-primary">Salvar Dado</button>
              </form>
            )}

            <table className="table">
              <thead>
                <tr>
                  <th>Nome</th>
                  <th>Chave</th>
                  <th>Valor</th>
                  <th>Status</th>
                  <th>Descrição</th>
                  <th>Ações</th>
                </tr>
              </thead>
              <tbody>
                {brokerData.map((data) => (
                  <tr key={data.id}>
                    <td>{data.dataName}</td>
                    <td><code className="code">{data.dataKey}</code></td>
                    <td>{data.dataValue}</td>
                    <td>
                      {data.isPreferred && <span className="badge badge-success">⭐ Preferencial</span>}
                    </td>
                    <td><small>{data.description}</small></td>
                    <td className="actions">
                      <button className="btn-icon delete" onClick={() => handleDelete(data.id)}>🗑️</button>
                    </td>
                  </tr>
                ))}
                {brokerData.length === 0 && !loading && (
                  <tr>
                    <td colSpan="6" style={{ textAlign: 'center', padding: '2rem' }}>
                      Nenhum dado cadastrado para este corretor.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}

      {!selectedBroker && (
        <div className="empty-state">
          <p>Selecione um corretor acima para gerenciar seus dados.</p>
        </div>
      )}
    </div>
  );
}
