import { useEffect, useState } from 'react'
import { getBots, createBot, updateBot, toggleBot } from '../services/api'


export default function Bots() {
  const [bots, setBots] = useState([])
  const [showModal, setShowModal] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [form, setForm] = useState({
    botNumber: '', botName: '', prompt: '', soul: '', isAgent: false, description: ''
  })
  const [editingId, setEditingId] = useState(null)

  const load = () => getBots().then(setBots)

  useEffect(() => { load() }, [])

  const handleSubmit = async e => {
    e.preventDefault()
    setLoading(true)
    setError('')
    try {
      if (editingId) {
        await updateBot(editingId, form)
      } else {
        await createBot(form)
      }
      handleCloseModal()
      load()
    } catch (err) {
      setError(err.response?.data?.error || 'Erro ao salvar bot.')
    } finally {
      setLoading(false)
    }
  }

  const handleOpenNew = () => {
    setEditingId(null)
    setForm({ botNumber: '', botName: '', prompt: '', soul: '', isAgent: false, description: '' })
    setShowModal(true)
  }

  const handleOpenEdit = bot => {
    setEditingId(bot.id)
    setForm({ botNumber: bot.botNumber, botName: bot.botName, prompt: bot.prompt, soul: bot.soul, isAgent: bot.isAgent, description: bot.description })
    setShowModal(true)
  }

  const handleCloseModal = () => {
    setShowModal(false)
    setEditingId(null)
    setForm({ botNumber: '', botName: '', prompt: '', soul: '', isAgent: false, description: '' })
  }

  const handleToggle = async id => {
    await toggleBot(id)
    load()
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">Bots</h1>
          <p className="page-sub">Gerencie os bots WhatsApp da plataforma</p>
        </div>
        <button className="btn btn-primary" onClick={handleOpenNew}>＋ Novo Bot</button>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: '16px' }}>
        {bots.length === 0 && (
          <div className="card" style={{ textAlign: 'center', color: 'var(--muted)', padding: '40px' }}>
            Nenhum bot cadastrado.
          </div>
        )}
        {bots.map(b => (
          <div className="card" key={b.id}>
            <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: '12px' }}>
              <div>
                <div style={{ fontWeight: 600, fontSize: '1rem' }}>{b.botName}</div>
                <div style={{ color: 'var(--muted)', fontSize: '0.82rem' }}>{b.botNumber}</div>
              </div>
              <span className={`badge ${b.isActive ? 'badge-green' : 'badge-red'}`}>
                {b.isActive ? 'Ativo' : 'Inativo'}
              </span>
            </div>
            <div style={{ marginBottom: '12px' }}>
              <span className={`badge ${b.isAgent ? 'badge-purple' : 'badge-blue'}`}>
                {b.isAgent ? 'AI Agent' : 'Standard Bot'}
              </span>
            </div>
            <div style={{ color: 'var(--muted)', fontSize: '0.82rem', marginBottom: '16px', lineHeight: '1.5' }}>
              {b.description || 'Sem descrição.'}
            </div>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                className="btn btn-sm btn-ghost"
                onClick={() => handleOpenEdit(b)}
              >
                Editar
              </button>
              <button
                className={`btn btn-sm ${b.isActive ? 'btn-danger' : 'btn-primary'}`}
                onClick={() => handleToggle(b.id)}
              >
                {b.isActive ? 'Desativar' : 'Ativar'}
              </button>
            </div>
          </div>
        ))}
      </div>

      {showModal && (
        <div className="modal-backdrop" onClick={handleCloseModal}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ width: '520px' }}>
            <div className="modal-title">{editingId ? 'Editar Bot' : 'Novo Bot'}</div>
            <form onSubmit={handleSubmit}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
                <div className="form-group">
                  <label>Nome do Bot</label>
                  <input value={form.botName} onChange={e => setForm(f => ({ ...f, botName: e.target.value }))} required placeholder="FinBot" />
                </div>
                <div className="form-group">
                  <label>Número WhatsApp</label>
                  <input value={form.botNumber} onChange={e => setForm(f => ({ ...f, botNumber: e.target.value }))} required placeholder="+5511999990001" />
                </div>
              </div>
              <div className="form-group">
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                  <input type="checkbox" checked={form.isAgent} onChange={e => setForm(f => ({ ...f, isAgent: e.target.checked }))} />
                  Atuar como Agente IA
                </label>
              </div>
              <div className="form-group">
                <label>Descrição</label>
                <input value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} required placeholder="Bot de atendimento imobiliário" />
              </div>
              <div className="form-group">
                <label>Soul (Personalidade / Tom de Voz)</label>
                <textarea rows={2} value={form.soul} onChange={e => setForm(f => ({ ...f, soul: e.target.value }))} required placeholder="Prestativa, formal e focada em conversão..." />
              </div>
              <div className="form-group">
                <label>Prompt Principal</label>
                <textarea rows={4} value={form.prompt} onChange={e => setForm(f => ({ ...f, prompt: e.target.value }))} required placeholder="Você é o assistente virtual da imobiliária XYZ..." />
              </div>
              {error && <p className="error">{error}</p>}
              <div className="modal-actions">
                <button type="button" className="btn btn-ghost" onClick={handleCloseModal}>Cancelar</button>
                <button type="submit" className="btn btn-primary" disabled={loading}>{loading ? 'Salvando...' : (editingId ? 'Salvar Alterações' : 'Criar Bot')}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
