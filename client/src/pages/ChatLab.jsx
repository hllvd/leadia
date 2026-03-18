import { useEffect, useRef, useState } from 'react'

const API_URL = '' // Use Vite proxy

async function sendMessage(brokerNumber, customerNumber, message, type = 'customer') {
  const res = await fetch(`/api/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ brokerNumber, customerNumber, message, type }),
  })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  return res.json()
}

function ChatWindow({ title, color, messages, input, setInput, onSend, loading, side }) {
  const bottomRef = useRef(null)

  const handleKey = e => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); onSend() }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minWidth: 0 }}>
      <div style={{
        padding: '12px 16px',
        background: color,
        borderRadius: '10px 10px 0 0',
        fontWeight: 600,
        fontSize: '0.9rem',
        color: '#fff',
        display: 'flex',
        alignItems: 'center',
        gap: 8,
      }}>
        {title}
      </div>

      <div style={{
        flex: 1,
        overflowY: 'auto',
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        padding: 16,
        background: 'var(--bg)',
        border: '1px solid var(--border)',
        borderTop: 'none',
        minHeight: 0,
      }}>
        {messages.length === 0 && (
          <div style={{ margin: 'auto', color: 'var(--muted)', fontSize: '0.85rem', textAlign: 'center' }}>
            Nenhuma mensagem ainda 👇
          </div>
        )}
        {messages.map((m, i) => (
          <div key={i} style={{
            alignSelf: m.sender === side ? 'flex-end' : 'flex-start',
            maxWidth: '78%',
            padding: '9px 13px',
            borderRadius: 12,
            fontSize: '0.87rem',
            lineHeight: 1.5,
            whiteSpace: 'pre-wrap',
            background: m.sender === side ? color : 'var(--surface2)',
            color: m.sender === side ? '#fff' : 'var(--text)',
            borderBottomRightRadius: m.sender === side ? 3 : 12,
            borderBottomLeftRadius: m.sender === side ? 12 : 3,
          }}>
            {m.text}
          </div>
        ))}
        {loading && (
          <div style={{
            alignSelf: 'flex-start',
            padding: '9px 13px',
            borderRadius: 12,
            fontSize: '0.87rem',
            background: 'var(--surface2)',
            color: 'var(--muted)',
          }}>
            Digitando…
          </div>
        )}
        <div ref={bottomRef} />
      </div>

      <div style={{
        display: 'flex',
        gap: 8,
        padding: '12px',
        background: 'var(--surface)',
        border: '1px solid var(--border)',
        borderTop: 'none',
        borderRadius: '0 0 10px 10px',
      }}>
        <input
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={handleKey}
          placeholder="Digite uma mensagem…"
          disabled={loading}
          style={{ flex: 1 }}
        />
        <button
          className="btn btn-primary"
          onClick={onSend}
          disabled={loading || !input.trim()}
        >
          Enviar
        </button>
      </div>
    </div>
  )
}

function FactsPanel({ facts, summary, allFacts, factLabels }) {
  const detected = allFacts.filter(k => facts[k] !== undefined)
  const missing  = allFacts.filter(k => facts[k] === undefined)

  return (
    <div style={{
      width: 260,
      flexShrink: 0,
      display: 'flex',
      flexDirection: 'column',
      gap: 12,
      overflowY: 'auto',
    }}>
      <div className="card" style={{ padding: 16 }}>
        <div style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: 8 }}>
          📝 Resumo
        </div>
        <p style={{ fontSize: '0.82rem', color: summary ? 'var(--text)' : 'var(--muted)', lineHeight: 1.5 }}>
          {summary || 'Aguardando mensagens suficientes para gerar resumo…'}
        </p>
      </div>

      <div className="card" style={{ padding: 16 }}>
        <div style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--green)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: 10 }}>
          ✅ Detectados ({detected.length})
        </div>
        {detected.length === 0 ? (
          <p style={{ fontSize: '0.82rem', color: 'var(--muted)' }}>Nenhum ainda</p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {detected.map(k => (
              <div key={k} style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <span style={{ fontSize: '0.72rem', color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                  {factLabels[k] ?? k}
                </span>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 6 }}>
                  <span style={{ fontSize: '0.85rem', color: 'var(--text)', fontWeight: 500 }}>
                    {String(facts[k].value)}
                  </span>
                  <span style={{
                    fontSize: '0.7rem',
                    padding: '1px 6px',
                    borderRadius: 100,
                    background: 'rgba(52,211,153,0.12)',
                    color: 'var(--green)',
                    fontWeight: 600,
                  }}>
                    {Math.round(facts[k].confidence * 100)}%
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="card" style={{ padding: 16 }}>
        <div style={{ fontSize: '0.75rem', fontWeight: 600, color: 'var(--yellow)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: 10 }}>
          ⏳ Não detectados ({missing.length})
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {missing.map(k => (
            <div key={k} style={{
              fontSize: '0.82rem',
              color: 'var(--muted)',
              padding: '4px 8px',
              borderRadius: 6,
              background: 'var(--surface2)',
            }}>
              {factLabels[k] ?? k}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

export default function ChatLab() {


  const [messages, setMessages] = useState([])
  const [facts, setFacts]       = useState({})
  const [summary, setSummary]   = useState('')
  const [allFacts, setAllFacts] = useState([])
  const [factLabels, setFactLabels] = useState({})

  const [customerInput, setCustomerInput] = useState('')
  const [brokerInput, setBrokerInput]     = useState('')
  const [loadingCustomer, setLoadingCustomer] = useState(false)
  const [loadingBroker, setBrokerLoading]     = useState(false)

  const [brokerNumber, setBrokerNumber]     = useState('5511999999999')
  const [customerNumber, setCustomerNumber] = useState('5511888888888')

  useEffect(() => {
    fetch('/api/chat/fact-metadata')
      .then(res => res.json())
      .then(data => {
        setAllFacts(data.keys || [])
        setFactLabels(data.labels || {})
      })
      .catch(err => console.error('Erro ao buscar metadados de fatos', err))
  }, [])

  const push = (sender, text) =>
    setMessages(prev => [...prev, { sender, text }])

  const conversationId = `${brokerNumber}-${customerNumber}`

  const sendAsCustomer = async () => {
    const text = customerInput.trim()
    if (!text || !brokerNumber || !customerNumber) return
    setCustomerInput('')
    push('customer', text)
    setLoadingCustomer(true)
    try {
      const data = await sendMessage(brokerNumber, customerNumber, text, 'customer')
      if (data.reply) push('broker', data.reply)
      if (data.facts?.length)  setFacts(Object.fromEntries(data.facts.map(f => [f.name, f])))
      if (data.summary)        setSummary(data.summary)
    } catch (err) {
      push('broker', `❌ Erro: ${err.message}`)
    } finally {
      setLoadingCustomer(false)
    }
  }

  const sendAsBroker = async () => {
    const text = brokerInput.trim()
    if (!text || !brokerNumber || !customerNumber) return
    setBrokerInput('')
    push('broker', text)
    setBrokerLoading(true)
    try {
      const data = await sendMessage(brokerNumber, customerNumber, text, 'broker')
      if (data.facts?.length)  setFacts(Object.fromEntries(data.facts.map(f => [f.name, f])))
      if (data.summary)        setSummary(data.summary)
    } catch (err) {
      push('broker', `❌ Erro: ${err.message}`)
    } finally {
      setBrokerLoading(false)
    }
  }

  return (
    <div>
      <div className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ flex: 1 }}>
          <h1 className="page-title">Chat Lab</h1>
          <p className="page-sub">Simule a conversa entre cliente e corretor — veja os fatos sendo extraídos em tempo real</p>
        </div>
        
        <div style={{ display: 'flex', gap: 12, alignItems: 'center', background: 'var(--surface)', padding: '8px 16px', borderRadius: 8, border: '1px solid var(--border)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <label style={{ fontSize: '0.65rem', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase' }}>Corretor</label>
            <input 
              value={brokerNumber} 
              onChange={e => setBrokerNumber(e.target.value)}
              placeholder="Número Corretor"
              style={{ padding: '4px 8px', fontSize: '0.85rem', width: 140 }}
            />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <label style={{ fontSize: '0.65rem', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase' }}>Cliente</label>
            <input 
              value={customerNumber} 
              onChange={e => setCustomerNumber(e.target.value)}
              placeholder="Número Cliente"
              style={{ padding: '4px 8px', fontSize: '0.85rem', width: 140 }}
            />
          </div>
          <button
            className="btn btn-ghost btn-sm"
            style={{ marginTop: 14 }}
            onClick={() => {
              setMessages([])
              setFacts({})
              setSummary('')
            }}
          >
            🔄 Reset
          </button>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 16, height: 'calc(100vh - 160px)', minHeight: 500 }}>
        <ChatWindow
          title="👤 Cliente"
          color="#7c6af7"
          messages={messages}
          input={customerInput}
          setInput={setCustomerInput}
          onSend={sendAsCustomer}
          loading={loadingCustomer}
          side="customer"
        />
        <ChatWindow
          title="🏠 Corretor (IA)"
          color="#059669"
          messages={messages}
          input={brokerInput}
          setInput={setBrokerInput}
          onSend={sendAsBroker}
          loading={loadingBroker}
          side="broker"
        />
        <FactsPanel facts={facts} summary={summary} allFacts={allFacts} factLabels={factLabels} />
      </div>
    </div>
  )
}
