---
title: 動態報表工作台的模型與硬體容量
status: draft
created: 2026-10-28
updated: 2026-10-28
revision: 繁體中文語句由語言模型處理；國語語音另用 Whisper，辨識結果以繁體文字送進對談
---

# 動態報表工作台的模型與硬體容量

30 人同時在線時，正式環境建議私有化部署 **Gemma 4 31B IT**（Google，Apache 2.0），以 **vLLM** 提供 OpenAI 相容端點，處理繁體中文語句。國語語音另用 **Whisper large-v3**（OpenAI）辨識成繁體文字，再送進同一條對談。語言模型用 **一張 48GB GPU（NVIDIA L40S 或同級）**，4-bit。語音用 **另一張 16GB GPU**。工作台與 MCP 另用一台不含 GPU 的應用主機。

模型權重與雲端 API 的發布者為美國或歐洲公司。下面的選擇都落在 Google、Meta、Mistral、OpenAI、Anthropic、Microsoft。

產品仍不綁定單一模型：`model.json` 只記錄位址與模型名稱，側車只打 OpenAI 相容的對話端點。

## 這套產品實際打到模型的方式

工作台在內容根的 `model.json` 設定模型。目前開發預設是本機 Ollama，模型是 Meta 的 Llama 3.2：

```json
{
  "enabled": true,
  "placement": "local",
  "endpoint": "http://127.0.0.1:11434/v1",
  "modelId": "llama3.2:latest",
  "apiKey": ""
}
```

側車（`DbServices.ModelSidecar`）用 `OpenAI.Chat.ChatClient` 對該位址送一次對話。正式路徑要求模型只回一個 JSON 物件，鍵是候選的連線、樣式與參數，不輸出 SQL。對談設計裡，模型一輪最多做兩次判斷：抽出關鍵字與條件值；字面比對不到已發布檢視時，才再對一次模組清單。取數仍走 MCP，不把查詢交給模型。

因此 30 人同時在線，不等於 30 條長文同時在生成。一人說完一句話之後，時間花在看表、留生成紀錄、匯出 PDF。同一秒真正在飛的模型請求，估計是 6 到 12 筆短呼叫。若 30 人同時各要一篇長摘要，容量要看升級線。

單次正式呼叫的規劃用量：

| 項目 | 規劃值 |
|------|--------|
| 輸入 | 約 1,500 token（句子、已發布檢視的名稱與條件） |
| 輸出 | 約 200 token 的 JSON |
| 一輪對談 | 1 到 2 次呼叫 |
| 上下文上限 | 8K，必要時 16K。不開模型原生的 256K |

敘事、版面建議、跨來源簡報沿用同一顆 31B。對談使用 instruct 權重。檢查點若有推理強度開關，正式對談調到最低，否則每句話會多出數秒到數十秒。

列內容與連線字串留在機房。語句與已發布檢視的名稱可以送到模型；查詢結果要寫進提示時，模型必須在私有化端點上。

## 模型

| 用途 | 選擇 | 發布者 | 說明 |
|------|------|--------|------|
| 正式環境 | **Gemma 4 31B IT**，4-bit | Google（美國） | Apache 2.0。約 30.7B、稠密、原生函式呼叫，上下文 256K，滑動視窗 1024。官方載入記憶體：BF16 69.9GB、FP8 34.9GB、Q4 17.5GB（含約兩成載入開銷，不含 KV）。2026-10 的 GGUF 實測：Q4_K_M 權重 17.1 GiB，單請求 8K 共 19.7 GiB、32K 共 21.6 GiB |
| 同一張卡要更低延遲 | **Gemma 4 26B A4B** | Google（美國） | MoE，總量約 26B，每 token 約啟動 4B。Q4 單請求 8K 約 17.4 GiB。JSON 穩定度用工作台語句確認後，可取代 31B |
| 品質與併發要再上一階 | **Mistral Small 4** | Mistral AI（法國） | Apache 2.0。119B 總量、約 6.5B 啟動，原生函式呼叫與 JSON，推理強度可依請求調低。Q4 在 8K 約 68GB，要 **1× H100 80GB**。KV 約 23KB／token，長上下文幾乎不吃顯存 |
| 雲端 | OpenAI、Google Gemini、Anthropic 或 Mistral 的 API | 美國、法國 | 只送句子與檢視目錄時，30 人的呼叫量很小。查詢結果改走雲端之前，先定資料外送政策 |
| 開發機 | 維持 Ollama 的 `llama3.2`，或改 **Gemma 4 12B** | Meta、Google | 單人試端點。12B 的 Q4 單請求 8K 約 8.3 GiB，行為較接近正式的 31B |

Gemma 4 31B 的滑動視窗讓 8K 到 32K 只多約 1.9 GiB（單請求、Q4）。48GB 卡扣掉約 17 GiB 權重後，剩餘空間給尖峰約 10 筆短請求。這個併發是依單請求實測外推，上線後用 vLLM 的 KV cache 使用率確認。

上線前用工作台的真實繁中語句驗 JSON：關鍵字、條件值、檢視名稱都要落在已發布清單裡。Llama 4 Scout（Meta）的 Q4 下載約 67GB，48GB 卡放不下，所以這台機器的文字模型用 Gemma 4 31B。

## 繁體中文與國語語音

文字與語音分成兩段。Gemma 4 31B 只收文字與圖像。模型卡寫明音訊只在 E2B、E4B、12B，而且 12B 那欄公開的語音成績註記排除了中文。國語辨識因此用 Whisper，辨識出的句子再交給 31B。

### 繁體中文文字

對談、JSON、敘事、檢視名稱與條件名都使用繁體中文。Gemma 4 模型卡寫明預訓練涵蓋 140 種以上語言，指令模型可直接使用的語言在 35 種以上。系統提示維持繁體，讓回覆沿用同一種字體。

上線前用工作台自己的句子驗收：一句繁體中文要抽出正確的關鍵字與條件值，檢視名稱必須落在已發布清單裡。簡體字出現時，先以 OpenCC 轉成繁體，再送進對談。

### 國語語音辨識

使用者說一句國語，服務把音訊轉成繁體文字，這段文字等同在 `/talk` 打的那一句，其後仍是現有的 JSON 判斷與 MCP 取數。音訊不進語言模型，也不進資料庫連線。

| 用途 | 選擇 | 發布者 | 說明 |
|------|------|--------|------|
| 正式環境 | **Whisper large-v3**，經 faster-whisper | OpenAI（美國）；推論引擎 SYSTRAN（法國） | 約 1.55B。語言設為中文，`initial_prompt` 給一句繁體中文，讓謄寫走繁體。OpenAI 在 Whisper 討論串示範過同一段音訊可依提示寫成繁體或簡體 |
| 雲端 | **Azure Speech**，語系 `zh-TW` | Microsoft（美國） | 文件語系名稱是 Chinese (Taiwanese Mandarin, Traditional)，快速轉錄有支援。音訊會離開機房，與列資料的外送政策一起決定 |
| 開發機 | 同一顆 Whisper large-v3，int8、CPU | OpenAI | 單人短句試聽。正式的同時辨識改走 16GB GPU |

faster-whisper 官方在 RTX 3070 Ti 上，以 large 模型、fp16、beam 5 轉錄 13 分鐘音訊，峰值顯存 4,525MB，耗時 1 分 03 秒。int8 峰值 2,926MB。GigaGPU 以英文 30 秒片段、要求每路維持即時（RTF ≤ 1）測得：RTX 4060 Ti 16GB 約 3 路 large-v3，RTX 5090 32GB 約 11 路。國語與英文共用同一顆 large-v3，顯存同級；字錯率要用台灣國語的句子另測。

報表語句多半是數秒到十餘秒。30 人在線時，同時開口以數路估計。16GB 卡服務這個部門；尖峰超過即時路數時，短句排隊數秒，再送進文字模型。

## 硬體

應用主機與推論主機分開。Blazor Server、MCP、SQLite 給 **8 核 CPU、32GB 記憶體**，服務 30 個工作台連線。文字 GPU 與語音 GPU 分開，vLLM 會預佔大部分顯存。

### 建議採購（30 人在線、短 JSON、尖峰約 10 筆同時推論、數路國語短句）

| 項目 | 規格 |
|------|------|
| 文字 GPU | **1× NVIDIA L40S 48GB**，或 RTX 6000 Ada 48GB。跑 Gemma 4 31B 的 4-bit |
| 語音 GPU | **1× 16GB**（RTX 4060 Ti 16GB 或 RTX 2000 Ada 16GB）。跑 Whisper large-v3 fp16 |
| CPU | 16 核 |
| 記憶體 | **128GB** |
| 磁碟 | 1TB NVMe。文字 4-bit 權重大約 18GB，Whisper 約 3GB，其餘留給映像與日誌 |
| 系統 | Linux。vLLM 以容器提供 `http://<主機>:8000/v1`。faster-whisper 另開一個語音端點 |
| 工作台設定 | `model.json` 的 `endpoint` 改到文字位址，`modelId` 改成所服務的模型名稱。語音端點另記，辨識完成的繁體句子才送進對談 |

較緊的預算可以把文字卡換成 **1× RTX 5090 32GB**，同樣跑 4-bit，`--max-model-len 8192`。單請求 8K 約 20 GiB，短 JSON 仍可服務這個部門，同時筆數比 48GB 少。30 人同時各要一篇長摘要時，這張卡會排隊。語音仍用另外那張 16GB。

FP8 權重官方約 34.9GB。48GB 卡上 FP8 留給 KV 的空間比 4-bit 少，要先量過尖峰併發再當正式精度。

### 升級線

改 **1× H100 80GB**，並改跑 **Mistral Small 4** 的 4-bit（8K 約 68GB），出現下列情況時：

- 尖峰同時推論穩定超過約 15 筆，而且輸出從 200 token 的 JSON 變成數百到上千 token 的敘事
- 31B 的繁中 JSON 在真實語句上不穩
- 對談路徑仍要開較高的推理強度

H100 上 Small 4 的 KV 很小，加長上下文時優先受權重影響，而不是受 256K 上限影響。服務時仍把 `--max-model-len` 訂在 8192 或 16384。

## 服務方式

正式環境用 vLLM 的 OpenAI 相容伺服器（`/v1/chat/completions`）。側車與 `model.json` 不用改協定，只改位址與模型名稱。

2026-03-25 在同一張 H100 上、以 Meta 的 Llama 3.1 8B 比較兩個引擎（提示約 100 token、輸出約 200 token）：32 筆同時請求時，vLLM 合計約 1,450 tok/s，Ollama 合計約 320 tok/s。單人時兩者接近，差在併發。Spheron 於 2026-10-27 仍引用該次測量。

服務參數從這裡開始，再依實際延遲調整：

- 模型：`google/gemma-4-31B-it` 的 4-bit 檢查點
- `--max-model-len 8192`（檢視目錄變大再升到 16384）
- 工具呼叫使用 vLLM 的 `--tool-call-parser gemma4`。需要嚴格 JSON 時，同時用 guided JSON 與提示裡的 JSON 指示
- 推理強度調到該檢查點的最低檔
- `--max-num-seqs` 先訂 16。KV cache 使用率長期高於九成時，先縮短上下文，再加 GPU

以 1,500 token 進、200 token 出估計，48GB 這台在中等併發下，一則對談應在數秒內回到工作台。上線後用 vLLM 的指標看 TTFT 與 KV cache 使用率，再用真實語句校正上表。

## 出處

測算與對照取自 2026-10-28 可讀的公開資料。換模型版本或 vLLM 版本時要重測顯存與延遲。

- Gemma 4 發布說明（Apache 2.0，31B 與 26B A4B）：<https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/>
- Gemma 4 模型卡（140 種以上語言；音訊只在 E2B、E4B、12B，12B 語音成績註記排除中文）：<https://huggingface.co/google/gemma-4-31B-it>
- 官方推論記憶體（含約兩成載入開銷，不含 KV）：<https://ai.google.dev/gemma/docs/core>
- 35 個模型的 GGUF 與單請求顯存（gpu.nz，2026-10-20）：<https://dev.to/gpunz/how-much-vram-do-open-llms-really-need-real-gguf-sizes-for-35-models-october-2026-28n2>
- Mistral Small 4 發布說明（Apache 2.0，2026-03-16）：<https://mistral.ai/news/mistral-small-4/>
- Mistral Small 4 顯存（2026-10-14，依 Hugging Face `config.json`）：<https://vramcalculator.net/models/mistral-small-4/>
- Llama 4 Scout 的 Q4 體積與單卡上限：<https://docs.tokios.com/models/llama-4>
- 函式呼叫與 `--tool-call-parser gemma4`（2026-10-10 讀取）：<https://eurokommerz.at/blog/choosing-an-open-llm-for-a-company-assistant/>
- Llama 3.1 8B、32 併發的 tok/s 對照（Spheron，測量日 2026-03-25）：<https://www.spheron.network/blog/ollama-vs-vllm/>
- Whisper 以 `initial_prompt` 指定繁體或簡體（OpenAI 討論串 #277）：<https://github.com/openai/whisper/discussions/277>
- faster-whisper 顯存（SYSTRAN，large、fp16 峰值 4,525MB）：<https://github.com/SYSTRAN/faster-whisper>
- Whisper large-v3 即時路數（GigaGPU，英文 30 秒片段）：<https://gigagpu.com/whisper-concurrent-streams-by-gpu/>
- Azure Speech 語系 `zh-TW`（Chinese (Taiwanese Mandarin, Traditional)，快速轉錄）：<https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support>
