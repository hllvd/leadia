import { useState, useEffect } from 'react';
import realStateService from '../../services/realStateService';
import AgencyForm from './AgencyForm';

export default function AgenciesList() {
  const [agencies, setAgencies] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [editingAgency, setEditingAgency] = useState(null);
  const [isFormOpen, setIsFormOpen] = useState(false);

  const fetchAgencies = async () => {
    try {
      setLoading(true);
      const res = await realStateService.getAgencies();
      setAgencies(res.data);
      setError(null);
    } catch (err) {
      setError('Erro ao carregar imobiliárias');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAgencies();
  }, []);

  const handleDelete = async (id) => {
    if (!window.confirm('Deseja excluir esta imobiliária?')) return;
    try {
      await realStateService.deleteAgency(id);
      fetchAgencies();
    } catch (err) {
      alert('Erro ao excluir imobiliária');
    }
  };

  const handleEdit = (agency) => {
    setEditingAgency(agency);
    setIsFormOpen(true);
  };

  const handleCreate = () => {
    setEditingAgency(null);
    setIsFormOpen(true);
  };

  const handleFormClose = (refresh) => {
    setIsFormOpen(false);
    if (refresh) fetchAgencies();
  };

  if (loading && agencies.length === 0) return <div className="loading">Carregando...</div>;

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <h1>Imobiliárias</h1>
          <p className="page-subtitle">Gerencie as imobiliárias e suas informações.</p>
        </div>
        <button className="btn btn-primary" onClick={handleCreate}>
          + Nova Imobiliária
        </button>
      </header>

      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <table className="table">
          <thead>
            <tr>
              <th>Nome</th>
              <th>Endereço</th>
              <th>Descrição</th>
              <th>Corretores</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>
            {agencies.map((agency) => (
              <tr key={agency.id}>
                <td><strong>{agency.name}</strong></td>
                <td>{agency.address}</td>
                <td>{agency.description}</td>
                <td>
                  <span className="badge">
                    {agency.brokerAssignments?.length || 0} associados
                  </span>
                </td>
                <td className="actions">
                  <button className="btn-icon" onClick={() => handleEdit(agency)} title="Editar">✏️</button>
                  <button className="btn-icon delete" onClick={() => handleDelete(agency.id)} title="Excluir">🗑️</button>
                </td>
              </tr>
            ))}
            {agencies.length === 0 && !loading && (
              <tr>
                <td colSpan="5" style={{ textAlign: 'center', padding: '2rem' }}>
                  Nenhuma imobiliária cadastrada.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {isFormOpen && (
        <AgencyForm 
          agency={editingAgency} 
          onClose={handleFormClose} 
        />
      )}
    </div>
  );
}
