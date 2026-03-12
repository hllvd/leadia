import { useState } from 'react';
import realStateService from '../../services/realStateService';

export default function AgencyForm({ agency, onClose }) {
  const [formData, setFormData] = useState({
    name: agency?.name || '',
    address: agency?.address || '',
    description: agency?.description || ''
  });
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      if (agency) {
        await realStateService.updateAgency(agency.id, formData);
      } else {
        await realStateService.createAgency(formData);
      }
      onClose(true);
    } catch (err) {
      alert('Erro ao salvar imobiliária');
      console.error(err);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="modal-overlay">
      <div className="modal-content card">
        <h3>{agency ? 'Editar Imobiliária' : 'Nova Imobiliária'}</h3>
        <form onSubmit={handleSubmit} className="form-stack">
          <div className="form-group">
            <label>Nome</label>
            <input 
              type="text" 
              value={formData.name} 
              onChange={(e) => setFormData({...formData, name: e.target.value})} 
              required 
              placeholder="Ex: Imobiliária Central"
            />
          </div>
          <div className="form-group">
            <label>Endereço</label>
            <input 
              type="text" 
              value={formData.address} 
              onChange={(e) => setFormData({...formData, address: e.target.value})} 
              required 
              placeholder="Rua, Número, Bairro, Cidade"
            />
          </div>
          <div className="form-group">
            <label>Descrição</label>
            <textarea 
              value={formData.description} 
              onChange={(e) => setFormData({...formData, description: e.target.value})} 
              placeholder="Informações adicionais sobre a imobiliária"
              rows="3"
            />
          </div>
          <div className="modal-actions">
            <button type="button" className="btn btn-secondary" onClick={() => onClose(false)}>
              Cancelar
            </button>
            <button type="submit" className="btn btn-primary" disabled={submitting}>
              {submitting ? 'Salvando...' : 'Salvar'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
