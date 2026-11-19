# Direção visual do Observatório

## Direção de arte da praça jogável em Gaussian splats

A referência aprovada para a praça é o painel esquerdo do [estudo de direção](../Art/Concepts/Observatorio/tres-direcoes-de-arte.png). A entrada enquadra a lua de latão sobre a cúpula de cobre e a entrega de um meteorito entre o pangolim e a ferreira. A pedra clara recebe sombras azuladas. O panorama tem menos contraste do que os personagens. As placas das estações próximas deixam livres os rostos e as ferramentas. A vista geral em V exibe as oito estações.

O pavilhão, as arcadas, a torre e os flamboyants são volumes em splats. O panorama e a [dupla da forja](../Art/Concepts/Observatorio/forge-duo.png) foram ilustrados com ImageGen a partir do painel aprovado. A dupla usa relevos orientados para a câmera. Para reproduzir a cena, gere os relevos com `python Art/Gaussian/painted-actors.py` (Pillow), depois rode `node Art/Gaussian/build_lumon_splats.mjs hub-world` e o método Unity `Lumon.HubWorld.Editor.HubSplatBuilder.InstallAndBuild`. Confira a tenaz e o focinho durante a entrega, a passagem dos jogadores e a lua inteira no enquadramento inicial.

### Acabamento das construções

As cinco estações ao redor da forja usam superfícies amostradas a 1,6 cm. A praça usa 4,1 cm no terreno. Os desenhos de pigmento acompanham as peças em coordenadas locais, para que as juntas das telhas, a trama do toldo e os veios da madeira permaneçam no lugar durante a órbita da câmera. Os volumes das estações também projetam sombra no piso.

Na escala de caminhada, a cobertura sobre a porta identifica a sede, a balança suspensa identifica a banca e o mostrador de temperatura explica a função do berço do ninho. As bobinas de latão acompanham os braços da máquina de fusão. O compasso repousa sobre a carta de expedições. No horizonte, a cúpula do pavilhão e a torre usam cobre patinado com placas sobrepostas. O sino ocupa a galeria aberta acima das janelas da torre. Nas lunetas Lira e Cisne, a objetiva fica embutida no aro e a pequena mira corre paralela ao tubo.

A modelagem das estações está em `Art/Gaussian/observatory.mjs` e a pintura em `Art/Gaussian/station-pigment.mjs`. A arquitetura do terraço e a óptica das lunetas estão em `Art/Gaussian/dawn-architecture.mjs`. Para conferir os materiais, compare cada construção na mesma posição e orientação de câmera, em 1080p ou na resolução nativa do monitor. O [percurso de revisão](../QA/Arquitetura-do-Observatorio/teste.txt) visita as oito interações e gira a câmera em torno das estações.

## Interface da praça jogável

Os painéis usam os tokens de cor de `Assets/Lumon/HubWorld/UI/Hub.uss`. Source Serif 4 Semibold identifica os títulos e Atkinson Hyperlegible é a fonte do texto de leitura. A marca, os botões e os marcadores são compostos sobre a cena renderizada na proporção do monitor.

## Arquivo do protótipo de interface ilustrada

As notas a seguir registram a fase anterior da interface. Para trabalho novo na praça, use a [skill de design do Lumon](../Plugins/lumon-design/skills/lumon-design/SKILL.md).

A praça usa o quadro fixo de 16:9. Os textos da interface continuam editáveis em PT-BR, EN e ES. Os letreiros da barraca e da mesa e o título do livro seguem áreas de quatro cantos medidas sobre a pintura no quadro de 1280×720. O jogo compõe cada palavra com os glifos do idioma selecionado.

### Tipografia

**Aurora Inscrita** é a fonte de títulos do Lumon. Foi desenhada para este projeto em uma grade de 500 × 700, com capitulares, versaletes, terminais cunhados, números e diacríticos. O arquivo TTF e a folha de glifos são gerados do mesmo desenho. A Atkinson Hyperlegible Next, no peso 450, atende instruções, rótulos e estados.

A altura e a largura de cada inscrição dentro da área pintada limitam o tamanho do texto. A barraca, a mesa e o livro têm coordenadas independentes. Um realce de 0,62 px abaixo da tinta acompanha a superfície inclinada.

| Papel | Valor |
| --- | --- |
| Tinta | #402232 |
| Texto secundário | #64424A |
| Papel e realce | #F6DFB9 e #FFE9C7 |
| Borda inferior da ação principal | #4C2830 |
| Seleção | #683549 |
| Foco | #E2AE59 |

### Livro da praça e bandeira da Equipe

A capa aproxima o letreiramento do desenho aprovado com pesos semelhantes nas duas linhas, recuo na segunda e tinta vinho sobre o papel. O Source Serif 4 Semibold recebe leve inclinação e reforço de traço na folha de glifos da capa. A composição reserva espaço para a bússola e seu N em todos os idiomas. A legenda em itálico está centrada sob a régua impressa. A faixa usa o Source Serif 4 Semibold em tom de marfim.

A bandeira da Equipe usa uma sequência de texturas gerada a partir da própria pintura da praça. As pontas junto ao mastro ficam fixas. A forma da inscrição segue a mesma curva das dobras. Em movimento reduzido, o tecido fica na pose dobrada.

Gere a folha de glifos do livro e a sequência do tecido com `python Art/HubWorld/render_field_book_and_cloth.py`, depois da captura da praça base descrita no [guia do HubWorld](HubWorld.md).
### Materiais e composição

O papel, os cartões e os botões usam grão derivado do pergaminho do Observatório. A sombra acompanha a silhueta do papel. Os botões têm borda iluminada em cima e espessura escura embaixo. Os retratos dos Lumons repousam em encaixes com tons de brasa, água e folha. A régua gravada com losango separa os títulos da leitura principal.

As configurações alinham título e rótulos pela margem esquerda. Os controles e as ações alinham pela direita. A equipe mantém retrato, nome e estado centrados em cada ficha. O atlas reserva a página esquerda para a cartografia e dá margens próprias à leitura e aos comandos na página direita.

Escape fecha ou cancela o painel. O prompt indica E na barraca da Equipe e F nas outras estações. O foco acompanha a navegação pelo teclado.

### Fontes e verificação

Os desenhos são reproduzíveis com os geradores abaixo. O primeiro usa Python com FontTools e Pillow. O segundo usa Pillow.

1. `python Art/HubWorld/build_aurora_inscrita.py`
2. `python Art/HubWorld/render_hub_materials.py`
3. `python Art/HubWorld/prepare_workshop_higgsfield.py` recorta as pranchas do Higgsfield e exporta os PNGs com transparência para o Unity. O gerador `render_workshop_fittings.py` contém os símbolos de navegação.

A [ficha de revisão](Entregas/HubWorld-Aceite-Visual.md) aponta para a build e suas capturas reais.

### Experiência visual da barraca

A arte dá a primeira leitura. A Brasa, a Gota e o Broto têm retratos nas listas de equipe, fusão e aprimoramento. Os ovos têm cores e selos próprios no balcão e na incubadora. A tela mostra preço, saldo, prazo e efeito da escolha junto ao objeto. A confirmação de fusão identifica o Lumon que será consumido.

A direção da barraca segue a referência enviada em 16/11/2026. O Higgsfield produziu a moldura de ouro esculpido, a gema de jade e os cartões elementais em duas pranchas sem texto. O interior azul-petróleo mantém áreas calmas atrás dos controles. Os retratos de Brasa, Gota e Broto ficam sobre seus próprios ambientes. Source Serif 4 aproxima o título, as abas e os comandos da referência. Atkinson Hyperlegible mantém preços, instruções e estado legíveis.

As abas têm símbolos próprios. Jade é a base responsiva dos controles. A tecla dourada identifica a seção ou opção selecionada. O cobre com folhas de ouro indica a ação principal, como salvar ou confirmar. Os controles iluminam no hover e afundam ao pressionar. O foco por teclado realça a silhueta do cartão ou o contorno da tecla. Os textos são compostos no Unity para PT-BR, EN e ES.

A entrada concentra a luz na moldura em 300 ms. O nascimento tem uma sequência própria de casca e retrato. Com movimento reduzido, a interface chega diretamente à pose legível. O controle fica disponível durante a revelação.

### Critérios para as próximas interfaces do Lumon

- Reconhecer a categoria e o elemento de ovos, Lumons, materiais e recompensas pela silhueta e pelo símbolo. Usar a cor como reforço. Conferir a arte no tamanho usado durante o jogo.
- Mostrar qual opção recebeu foco, qual foi escolhida e quando a ação concluiu. Manter o objeto escolhido visível enquanto preço, quantidade ou estado mudam.
- Em ações que consomem recursos, manter o item, a quantidade e o efeito da transação no mesmo quadro. Mostrar os detalhes no ponto em que mudam a decisão.
- Dar uma entrada curta às telas que abrem no mundo. Reservar sequências maiores para transformações como eclosão, fusão e recompensas. Chegar ao mesmo resultado com movimento reduzido e manter uma saída por teclado.
- Compor texto no jogo em PT-BR, EN e ES. Capturar cada tela no executável, inspecionar legibilidade em tamanho real e percorrer a interação completa com mouse e teclado.
