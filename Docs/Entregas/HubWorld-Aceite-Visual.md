# Observatório da Aurora · revisão visual

O aceite visual foi reaberto após a rejeição da entrega anterior. A candidata mantém a composição aprovada da praça e incorpora a fonte Aurora Inscrita, o letreiramento projetado e os novos materiais da interface. O aceite visual fica com o usuário.

## Responsabilidades

| Frente | Responsável | Evidência |
| --- | --- | --- |
| UX, direção visual e integração | Codex | Margens, alinhamento, tipografia e profundidade revisados nas capturas do executável. |
| Engenharia | Fable 5.2 | Circulação e interações da candidata anterior, exercitadas novamente. |
| Animação | Opus 5.5 | Curvas das lanternas, chama e vento, integradas e verificadas no executável. |
| Aceite visual | Victo | Revisão da candidata no executável e em movimento. |

A ficha adota o fluxo do Claude Code Game Studios para projetos existentes. Cada requisito abaixo tem evidência própria. O executável é a referência para avaliar a aparência em jogo. [Claude Code Game Studios](https://github.com/donchitos/claude-code-game-studios)

## Evidências da candidata

| Requisito | Resultado verificável |
| --- | --- |
| Praça | As inscrições da mesa e da barraca seguem quatro cantos medidos na pintura. O título do livro se ajusta à página e reserva altura para duas linhas. A aplicação compõe as inscrições a partir dos glifos do idioma ativo. [Praça PT](../../Art/HubWorld/review/design/design-pt-plaza.png) · [Praça ES em 1080p](../../Art/HubWorld/review/player-praca-es-1080.png) |
| Equipe | Retratos, nomes e estados têm hierarquia própria e posições centradas. Os três encaixes circulares dão apoio visual aos retratos, sobre fichas de papel com chanfro. [Equipe](../../Art/HubWorld/review/design/design-pt-team.png) |
| Configurações | Título e rótulos partem da margem esquerda. Controles e ações alinham pela direita. Pergaminho, botões e foco têm tratamentos de relevo próprios. [Configurações](../../Art/HubWorld/review/design/design-pt-settings.png) |
| Atlas | O mapa ocupa a página esquerda. Título, régua gravada, descrição, estado e ações usam a área útil da página direita. [Atlas PT](../../Art/HubWorld/review/design/design-pt-atlas.png) · [Atlas EN](../../Art/HubWorld/review/design/design-en-atlas.png) |
| Tipografia e localização | A fonte de títulos Aurora Inscrita foi desenhada para o Lumon. A Atkinson Hyperlegible Next atende instruções e estados. Ambas são carregadas na build. As 12 combinações de tela e idioma foram medidas em 1280×720, 1440×900 e 1920×1080. [Relatório 16:9](../../Art/HubWorld/review/design/player-design.txt) · [Relatório 16:10](../../Art/HubWorld/review/design/player-design-wide.txt) · [Relatório 1080p](../../Art/HubWorld/review/design/player-design-1080.txt) |
| Lanternas | As suspensas balançam pela fixação com a luz acompanhando a estrutura. A luz da lanterna de mesa pulsa sobre o suporte fixo. [Praça em 1280×720](../../Art/HubWorld/review/player-praca-em-movimento.webp) · [Detalhes em movimento](../../Art/HubWorld/review/player-ambiente.gif) |
| Árvores e fogueira | Rajadas deformam a copa com a base apoiada. A chama varia a silhueta e lança brasas, com oclusão da Guardiã. Com movimento reduzido, os sete recortes permanecem estáticos. [Medição da captura](../../Art/HubWorld/review/player-motion.json) |
| Regressões | Circulação, colisão, oclusão, ações de interface, áudio e recarga passaram no roteiro interno do executável. O reteste com eventos de teclado e mouse do Windows também passou. [Entrada](../../Art/HubWorld/review/player-input.txt) [Percurso](../../Art/HubWorld/review/player-smoke.txt) · [Áudio](../../Art/HubWorld/review/player-audio.txt) |

A [build Windows x64](../../Builds/HubWorld-Win64/Lumon.exe) entra no Observatório. A validação de interface e movimento pode ser repetida com `Art/HubWorld/review_hub_craft.ps1`. A [direção visual](../HubWorld-Design.md) documenta as fontes, paleta e espaçamento.

**Estado** — HubWorld aprovado visualmente em 16/11/2026, incluindo a capa final do Vale da Nascente e a bandeira da Equipe. [Build e evidências](../../Art/HubWorld/review/release.json)
