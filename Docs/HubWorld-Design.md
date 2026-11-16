# Interface do Observatório

A praça usa o quadro fixo de 16:9. Os textos da interface continuam editáveis em PT-BR, EN e ES. Os letreiros da barraca e da mesa e o título do livro seguem áreas de quatro cantos medidas sobre a pintura no quadro de 1280×720. O jogo compõe cada palavra com os glifos do idioma selecionado.

## Tipografia

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

## Livro da praça e bandeira da Equipe

A capa aproxima o letreiramento do desenho aprovado com pesos semelhantes nas duas linhas, recuo na segunda e tinta vinho sobre o papel. O Source Serif 4 Semibold recebe leve inclinação e reforço de traço na folha de glifos da capa. A composição reserva espaço para a bússola e seu N em todos os idiomas. A legenda em itálico está centrada sob a régua impressa. A faixa usa o Source Serif 4 Semibold em tom de marfim.

A bandeira da Equipe usa uma sequência de texturas gerada a partir da própria pintura da praça. As pontas junto ao mastro ficam fixas. A forma da inscrição segue a mesma curva das dobras. Em movimento reduzido, o tecido fica na pose dobrada.

Gere a folha de glifos do livro e a sequência do tecido com `python Art/HubWorld/render_field_book_and_cloth.py`, depois da captura da praça base descrita no [guia do HubWorld](HubWorld.md).
## Materiais e composição

O papel, os cartões e os botões usam grão derivado do pergaminho do Observatório. A sombra acompanha a silhueta do papel. Os botões têm borda iluminada em cima e espessura escura embaixo. Os retratos dos Lumons repousam em encaixes com tons de brasa, água e folha. A régua gravada com losango separa os títulos da leitura principal.

As configurações alinham título e rótulos pela margem esquerda. Os controles e as ações alinham pela direita. A equipe mantém retrato, nome e estado centrados em cada ficha. O atlas reserva a página esquerda para a cartografia e dá margens próprias à leitura e aos comandos na página direita.

Escape fecha ou cancela o painel. O prompt indica E na barraca da Equipe e F nas outras estações. O foco acompanha a navegação pelo teclado.

## Fontes e verificação

Os desenhos são reproduzíveis com os geradores abaixo. O primeiro usa Python com FontTools e Pillow. O segundo usa Pillow.

1. `python Art/HubWorld/build_aurora_inscrita.py`
2. `python Art/HubWorld/render_hub_materials.py`
3. `python Art/HubWorld/prepare_workshop_higgsfield.py` recorta as pranchas do Higgsfield e exporta os PNGs com transparência para o Unity. O gerador `render_workshop_fittings.py` contém os símbolos de navegação.

A [ficha de revisão](Entregas/HubWorld-Aceite-Visual.md) aponta para a build e suas capturas reais.

## Experiência visual da barraca

A arte dá a primeira leitura. A Brasa, a Gota e o Broto têm retratos nas listas de equipe, fusão e aprimoramento. Os ovos têm cores e selos próprios no balcão e na incubadora. A tela mostra preço, saldo, prazo e efeito da escolha junto ao objeto. A confirmação de fusão identifica o Lumon que será consumido.

A direção da barraca segue a referência enviada em 16/11/2026. O Higgsfield produziu a moldura de ouro esculpido, a gema de jade e os cartões elementais em duas pranchas sem texto. O interior azul-petróleo mantém áreas calmas atrás dos controles. Os retratos de Brasa, Gota e Broto ficam sobre seus próprios ambientes. Source Serif 4 aproxima o título, as abas e os comandos da referência. Atkinson Hyperlegible mantém preços, instruções e estado legíveis.

As abas têm símbolos próprios. Jade é a base responsiva dos controles. A tecla dourada identifica a seção ou opção selecionada. O cobre com folhas de ouro indica a ação principal, como salvar ou confirmar. Os controles iluminam no hover e afundam ao pressionar. O foco por teclado realça a silhueta do cartão ou o contorno da tecla. Os textos são compostos no Unity para PT-BR, EN e ES.

A entrada concentra a luz na moldura em 300 ms. O nascimento tem uma sequência própria de casca e retrato. Com movimento reduzido, a interface chega diretamente à pose legível. O controle fica disponível durante a revelação.

## Critérios para as próximas interfaces do Lumon

- Reconhecer a categoria e o elemento de ovos, Lumons, materiais e recompensas pela silhueta e pelo símbolo. Usar a cor como reforço. Conferir a arte no tamanho usado durante o jogo.
- Mostrar qual opção recebeu foco, qual foi escolhida e quando a ação concluiu. Manter o objeto escolhido visível enquanto preço, quantidade ou estado mudam.
- Em ações que consomem recursos, manter o item, a quantidade e o efeito da transação no mesmo quadro. Mostrar os detalhes no ponto em que mudam a decisão.
- Dar uma entrada curta às telas que abrem no mundo. Reservar sequências maiores para transformações como eclosão, fusão e recompensas. Chegar ao mesmo resultado com movimento reduzido e manter uma saída por teclado.
- Compor texto no jogo em PT-BR, EN e ES. Capturar cada tela no executável, inspecionar legibilidade em tamanho real e percorrer a interação completa com mouse e teclado.
