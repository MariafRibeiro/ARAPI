# Frontend Flet

Cliente Flet para consultar e sincronizar os deputados da API ARAPI.

## Executar API e Flet juntos

A partir da raiz do projeto (`E:\git projetos\ARAPI-1`), execute:

```powershell
.\start-dev.ps1
```

O script inicia a API .NET em `http://localhost:5030` e o frontend Flet ao
mesmo tempo. Ele cria o ambiente virtual e instala as dependências se ainda
não existirem.

Se o PowerShell bloquear a execução do script, rode apenas para esta sessão:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\start-dev.ps1
```

## Executar

Com a API .NET executando em `http://localhost:5030`, execute a partir da
raiz do projeto:

```powershell
cd frontend
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe main.py
```

Se estiver na raiz do projeto e o ambiente virtual já tiver sido criado dentro
de `frontend`, use:

```powershell
.\frontend\.venv\Scripts\python.exe frontend\main.py
```

Se o terminal já estiver em `E:\git projetos\ARAPI-1\frontend`, não execute
`cd frontend`; use diretamente:

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe main.py
```

Não é necessário executar `Activate.ps1`. O comando acima usa diretamente o
Python do ambiente virtual e funciona mesmo com a política de execução do
PowerShell restrita.

Para usar outra URL da API:

```powershell
$env:ARAPI_URL = "http://localhost:5030"
.\frontend\.venv\Scripts\python.exe .\frontend\main.py
```
