# CRM Inteligente para Imobiliárias com Automação de WhatsApp
## Visão Geral do Produto

Este produto é um **CRM inteligente focado em imobiliárias**, cuja principal função é **automatizar a gestão de leads vindos do WhatsApp e transformar conversas em ações automáticas dentro do CRM**.

A ideia central é simples:

> Toda conversa no WhatsApp vira **dados estruturados**, **tarefas**, **leads qualificados**, **lembretes** e **oportunidades de venda**.

O sistema não depende de o corretor preencher nada manualmente.  
A própria **IA interpreta a conversa e atualiza o CRM automaticamente.**

Isso resolve um dos maiores problemas do mercado imobiliário:

- Corretores esquecem de registrar informações
- Leads são perdidos
- Follow-ups não acontecem
- Imobiliária não tem dados confiáveis

O robô faz tudo isso automaticamente.

### Assistente de Conversa para Corretores

Além de organizar dados, o sistema também analisa a qualidade da comunicação do corretor com o cliente, oferecendo sugestões para melhorar a conversão.

Exemplo:

**Fala do Corretor**: "_Vou deixar você analisar as fotos com calma e, quando quiser visitar, é só me chamar._"
**Insight da IA**: Você entregou o controle da jornada ao cliente e perdeu a autoridade. O corretor de elite conduz o processo dessa forma: 

"_Vou te dar 48h para avaliar com sua esposa. Na quarta-feira, às 10h, te ligo para entendermos se esse é o imóvel que atende sua expectativa ou se ajustamos a busca._" 

---

# Lista Completa de Features do Robô (Prioridade de Execução)

As funcionalidades estão listadas **na ordem de prioridade operacional**.

---

# 1. Captura de Mensagens do WhatsApp ( modo listening)

O robô sempre está no modo listening **para novos leads**. Isso significa que o sistema monitora conversas do WhatsApp em tempo real.

Quando uma nova mensagem chega:

1. Identifica o **número do cliente**
2. Associa a um **lead existente**
3. Ou cria **novo lead automaticamente**

Informações armazenadas:

- telefone
- nome (se disponível)
- corretor responsável
- origem
- timestamp
- Email
- Tipo de lead
    - comprador
    - vendedor
    - investidor
    - locação
    - curioso
    - suporte
    - Outro — mensagem não relacionada a compra, venda ou locação de imóveis
- Lead Score
- histórico completo da conversa

---
# 2. Robô se ativa o modo agente

Se nenhuma resposta for enviada em até 3 minutos, ou se uma mensagem automática predefinida (como “Oi, estou buscando informações de investimento”) for detectada — conforme a configuração do corretor — o robô entra em modo agente.

Nesse modo, ele conduz o cliente por uma sequência de perguntas e tenta identificar automaticamente as **10 perguntas fundamentais sobre cada conversa**.

---

# 3. Extração de Informações da Conversa

A IA identifica e responde **10 perguntas fundamentais sobre cada conversa**.

Essas informações ajudarão a estruturar os dados no CRM. Tanto no modo _Listening_ quanto no modo _Agente_, o robô analisará a conversa e tentará extrair essas respostas de forma contínua.

## Perguntas

1. O lead deseja **comprar, vender, alugar ou investir**?

2. Qual é o **tipo de imóvel** de interesse?

- apartamento  
- casa  
- terreno  
- comercial  

3. Qual **bairro ou região** o lead procura?

4. Qual é a **faixa de preço** desejada?

5. O lead possui **financiamento aprovado**?

6. O imóvel é para **moradia ou investimento**?

7. Qual é o **prazo estimado para a compra**?

- urgente  
- até 3 meses  
- até 6 meses  
- apenas pesquisando  

8. Existe **interesse em agendar uma visita**?

9. O lead mencionou **algum imóvel específico**?

10. Qual é o **nível de interesse do lead (Lead Score)**?

## Lead Score

O nível de interesse é calculado automaticamente com base nas interações do lead:

- respondeu à primeira mensagem → **+2**
- respondeu com mensagem de áudio → **+10**
- perguntou sobre preço → **+20**
- solicitou visita → **+40**
- enviou documentos → **+80**

A lista de leads com seus respectivos **scores** ficará disponível para o corretor no aplicativo do corretor, onde será possível **editar informações, remover registros ou adicionar novos itens** conforme necessário.

## Guardar resumo conciso da conversa.

Exemplo:

```
11/03 12:35:12 - Lead chegou
11/03 12:37:02- Primeira conversa
13/03 08:44:00- Cliente pediu visita
13/03 09:02:37- Visita marcada
14/03 10:02:37- Nova Visita marcada
18/03 08:05:03- Proposta enviada
```

---

# 4. Criação Automática e Atualização de Lead no CRM

Se o número não existir no CRM:

Criar automaticamente:
```
Lead {
    telefone
    nome (se disponível)
    corretor responsável
    origem
    timestamp
    Email
    Tipo de lead
        comprador
        vendedor
        investidor
        locação
        curioso
        suporte
        Outro — mensagem não relacionada a compra, venda ou locação de imóveis
    Lead Score
    histórico completo da conversa
}
```

Se o cliente já existir:

Atualizar:

- histórico de conversa
- estágio do funil
- interesse detectado

Isso evita que **leads sejam perdidos**.


---

# 5. Criação Automática de Tarefas

O sistema será capaz de identificar intenções e tarefas nas conversas entre o corretor e o cliente.

#### Se detectar que não houve uma resposta por alguma das partes :

Uma notificação chegará ao corretor com a mensagem:
>João está esperando uma resposta sua sobre as fotos (479822321)

ou

>José ainda não respondeu sobre o bairro.

#### Se detectar uma intenção de visita

Uma nova tarefa será criada do tipo:
>Visita agendada com João no dia 12/04 as  15:30

Todos os dias, o corretor recebe uma mensagem pela manhã com as tarefas:
>Tarefas de hoje: 
* Ligar para Amandar (470901239)
* Enviar documento para Claudio (119399384)
* Fazer orçamento de material de construção para apartamento do Garden (Luciana)

Esta TODO list estará disponível no app do corretor, onde o corretor vai poder gestionar a lista

A lista de agendamentos vai poder ser acessar pelo corretor e gestionada por ele através de umlink que irá direto ao app.

---

# 6. Lembrete Automático da Visita

O sistema envia mensagens automáticas:

### Para o cliente

1 dia antes
>Olá! Confirmando nossa visita amanhã às 16h.

2 horas antes
>Estamos confirmados para a visita hoje às 16h.

### Para o corretor
Criar um item na TODO list enviada no começo do dia do tipo:
>Lembrete: visita com João hoje às 16h


---

# 7. Envio de E-mail automático no fim da conversa  
_(3 horas após a conversa ou no período da noite)_

Após o primeiro contato, a IA criará automaticamente um e-mail com um **resumo da conversa** e das principais informações coletadas.

```
Assunto: Resumo da nossa conversa sobre imóveis

Boa tarde João,

Obrigado pelo seu tempo hoje. Segue um breve resumo da nossa conversa:

📌 Objetivo: Compra de imóvel
🏠 Tipo de imóvel: Apartamento
📍 Região de interesse: Bairro X
💰 Faixa de preço: Até R$ 600.000
📅 Prazo estimado: Até 6 meses
👀 Interesse em visita: Sim

Com base nessas informações, vamos preparar algumas opções de imóveis que se encaixam no seu perfil.

Caso queira atualizar alguma informação ou agendar uma visita, é só responder este e-mail ou continuar a conversa pelo WhatsApp.

Fico à disposição para ajudar no que precisar.

Atenciosamente,
Equipe Imobiliária

```
O conteúdo do e-mail é **gerado automaticamente com base nas informações extraídas da conversa**, ajudando o cliente a confirmar os dados e mantendo um registro claro do atendimento.

---

## 8. Dashboard de Inteligência Comercial

A imobiliária pode ver:

- quantos leads chegaram
- quantos viraram visita
- quantos viraram venda
- taxa de resposta do corretor
- a aptidadão de cada corretor pelos processo do Funil

---

## 9. Reativação Inteligente de Leads Antigos

A maioria das imobiliárias possui centenas de leads antigos que nunca foram trabalhados novamente.

O agente analisa automaticamente toda a base de conversas antigas e identifica **oportunidades de reativação**.

O sistema cruza:

- histórico de conversa do lead
- preferências extraídas pela IA (bairro, tipo de imóvel, preço, objetivo)
- novos imóveis cadastrados no sistema

Exemplo:

Lead de 8 meses atrás  
→ buscava **apartamento no bairro X**  
→ faixa de preço **até R$ 500 mil**

Quando um novo imóvel compatível aparece, o sistema detecta automaticamente o match.

### O que acontece quando há um match

1. A IA cria um **resumo do histórico do lead**  
2. Sugere o contato em uma **lista de oportunidades para o corretor**  
3. Opcionalmente envia uma **mensagem personalizada de reativação**

Exemplo de mensagem:

> Olá João!  
> Alguns meses atrás você comentou que estava procurando um apartamento no bairro X.  
> Acabamos de receber um imóvel que pode encaixar no seu perfil.  
> Quer que eu te envie mais detalhes?

### Resultado

- Recuperação de **leads esquecidos**
- Aproveitamento de **todo o histórico de conversas**
- Novas vendas geradas a partir de **leads antigos**

---

# Funil Imobiliário Ideal (7 Etapas)


## 1. Novo Lead
O lead acabou de chegar ao seu sistema.

* **Origem:**
    * WhatsApp
    * Portal imobiliário
    * Instagram
    * Indicação
* **Objetivo:** * Responder rápido
    * Iniciar conversa
* **Automação possível:**
    * Mensagem automática
    * Coleta de informações básicas
* **Exemplo de dados coletados:**
    * Tipo de imóvel
    * Bairro
    * Faixa de preço

---

## 2. Lead Engajado
O lead respondeu e iniciou a conversa de fato.

* **Sinais:**
    * Respondeu perguntas
    * Pediu informações
    * Demonstrou curiosidade
* **Objetivo:**
    * Entender necessidade
    * Qualificar o lead
* **Aqui o CRM pode gerar:**
    * Resumo automático da conversa
    * Primeiro *lead score*

---

## 3. Lead Qualificado
Agora já sabemos que o lead é real e tem perfil.

* **Informações conhecidas:**
    * Tipo de imóvel
    * Faixa de preço
    * Região de interesse
    * Forma de pagamento
* **Objetivo:**
    * Apresentar imóveis relevantes
* **Automação poderosa:**
    * Recomendação automática de imóveis
* **Exemplo:**
    * *"Encontrei 3 imóveis que combinam com o que você procura."*

---

## 4. Imóvel de Interesse
O lead demonstrou interesse real em um ou mais imóveis específicos.

* **Sinais:**
    * Pediu fotos/vídeo
    * Perguntou preço/condomínio
    * Perguntou sobre financiamento
* **Objetivo:**
    * Aprofundar interesse
* **No CRM:**
    * Registro de *lead score* por imóvel

---

## 5. Visita Agendada
Um dos momentos mais críticos da jornada.

* **Sinais:**
    * Solicitou visita
    * Combinou horário
* **Objetivo:**
    * Garantir que a visita aconteça
* **Automação útil:**
    * Lembrete automático
    * Confirmação no dia da visita

---

## 6. Negociação
Lead visitou o imóvel e está avaliando a compra/locação.

* **Sinais:**
    * Perguntou sobre proposta
    * Perguntou sobre documentação
    * Negociação de preço
* **Objetivo:**
    * Fechar acordo
* **Automação possível:**
    * Registro de proposta
    * Histórico de negociação

---

## 7. Fechamento
Venda realizada com sucesso.

* **Ações:**
    * Registrar venda
    * Atualizar funil
    * Iniciar pós-venda
* **Automação:**
    * Mensagem de agradecimento
    * Pedido de indicação
    * Pedido de avaliação

---

### Representação simples do funil

**Novo Lead**
>↓

**Lead Engajado**
>↓

**Lead Qualificado**
>↓

**Imóvel de Interesse**
>↓

**Visita Agendada**
>↓

**Negociação**
>↓

**Fechamento**

---

### 💡 Insight MUITO importante
Seu CRM pode calcular a **taxa de conversão** entre etapas para mostrar onde a imobiliária está perdendo vendas.

| Etapa | Leads |
| :--- | :--- |
| Novo Lead | 100 |
| Engajado | 80 |
| Qualificado | 50 |
| Interesse | 30 |
| Visita | 15 |
| Negociação | 7 |
| **Venda** | **3** |

---

# Conclusão

Esse produto resolve um problema gigante:

**corretores não alimentam o CRM.**

A IA faz isso automaticamente.

Principais benefícios:

- nenhum lead perdido
- CRM sempre atualizado
- automação de follow-up
- geração de vendas com leads antigos
- inteligência comercial
