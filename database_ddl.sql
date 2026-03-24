--
-- PostgreSQL database dump
--

-- Dumped from database version 14.18
-- Dumped by pg_dump version 14.18

-- Started on 2026-03-24 13:16:31

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 20 (class 2615 OID 796583)
-- Name: eventual; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA eventual;


--
-- TOC entry 23 (class 2615 OID 799606)
-- Name: web; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA web;


--
-- TOC entry 731 (class 1255 OID 816845)
-- Name: fn_analista_obrigatorio(); Type: FUNCTION; Schema: eventual; Owner: -
--

CREATE FUNCTION eventual.fn_analista_obrigatorio() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    -- analista must be present whenever status is NOT AGUARDANDO_ANALISE
    IF NEW.status <> 'AGUARDANDO_ANALISE' THEN
        IF NEW.analista IS NULL OR btrim(NEW.analista) = '' THEN
            RAISE EXCEPTION 'analista cannot be NULL when status = %', NEW.status;
        END IF;
    END IF;

    RETURN NEW;
END;
$$;


--
-- TOC entry 728 (class 1255 OID 816843)
-- Name: fn_evitar_status_repetido(); Type: FUNCTION; Schema: eventual; Owner: -
--

CREATE FUNCTION eventual.fn_evitar_status_repetido() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    ultimo RECORD;
BEGIN
    SELECT *
    INTO ultimo
    FROM eventual.fluxo_pendencia fp
    WHERE fp.entidade_tipo = NEW.entidade_tipo
      AND fp.entidade_id   = NEW.entidade_id
    ORDER BY fp.criado_em DESC
    LIMIT 1;

    IF ultimo IS NOT NULL THEN
        IF ultimo.status = NEW.status THEN
            RAISE EXCEPTION
                'Cannot insert duplicate status %. Last status for this pendência is already %.',
                NEW.status, ultimo.status;
        END IF;
    END IF;

    RETURN NEW;
END;
$$;


--
-- TOC entry 729 (class 1255 OID 816935)
-- Name: fn_motivo_obrigatorio(); Type: FUNCTION; Schema: eventual; Owner: -
--

CREATE FUNCTION eventual.fn_motivo_obrigatorio() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    -- motivo required ONLY when rejecting
    IF NEW.status IN ('REJEITADO') THEN
        IF NEW.motivo IS NULL OR btrim(NEW.motivo) = '' THEN
            RAISE EXCEPTION 'motivo cannot be NULL or empty when status = %', NEW.status;
        END IF;
    END IF;

    RETURN NEW;
END;
$$;


--
-- TOC entry 727 (class 1255 OID 816841)
-- Name: fn_valida_entidade(); Type: FUNCTION; Schema: eventual; Owner: -
--

CREATE FUNCTION eventual.fn_valida_entidade() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.entidade_tipo = 'EMPRESA' THEN
        IF NOT EXISTS (SELECT 1 FROM geral.empresa e WHERE e.cnpj::text = NEW.entidade_id) THEN
            RAISE EXCEPTION 'Entidade EMPRESA % does not exist', NEW.entidade_id;
        END IF;
    ELSIF NEW.entidade_tipo = 'VEICULO' THEN
        IF NOT EXISTS (SELECT 1 FROM geral.veiculo v WHERE v.placa = NEW.entidade_id) THEN
            RAISE EXCEPTION 'Entidade VEICULO % does not exist', NEW.entidade_id;
        END IF;
    ELSIF NEW.entidade_tipo = 'MOTORISTA' THEN
        IF NOT EXISTS (SELECT 1 FROM eventual.motorista m WHERE m.id::text = NEW.entidade_id) THEN
            RAISE EXCEPTION 'Entidade MOTORISTA % does not exist', NEW.entidade_id;
        END IF;
    ELSE
        RAISE EXCEPTION 'Unknown entidade_tipo: %', NEW.entidade_tipo;
    END IF;
    RETURN NEW;
END;
$$;


--
-- TOC entry 730 (class 1255 OID 816852)
-- Name: normalizar_email_usuario(); Type: FUNCTION; Schema: web; Owner: -
--

CREATE FUNCTION web.normalizar_email_usuario() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.email IS NOT NULL THEN
        -- Normalize: lowercase, trim, and keep only a-z, 0-9, ., _, -, +
--        NEW.email := lower(
--            regexp_replace(
--                btrim(NEW.email),
--                '[^a-z0-9._+-]',
--                '',
--                'g'
--            )
--        );
		NEW.email := lower(NEW.email);
    END IF;

    RETURN NEW;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 439 (class 1259 OID 800010)
-- Name: documento; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.documento (
    id integer NOT NULL,
    documento_tipo_nome text NOT NULL,
    tamanho bigint,
    hash character(32),
    data_upload timestamp without time zone DEFAULT now(),
    validade date,
    fluxo_pendencia_id integer,
    aprovado_em timestamp without time zone,
    conteudo bytea NOT NULL,
    nome_arquivo text NOT NULL,
    content_type text DEFAULT 'application/pdf'::text NOT NULL
);


--
-- TOC entry 440 (class 1259 OID 800024)
-- Name: documento_empresa; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.documento_empresa (
    id integer NOT NULL,
    empresa_cnpj text NOT NULL
);


--
-- TOC entry 438 (class 1259 OID 800009)
-- Name: documento_id_seq; Type: SEQUENCE; Schema: eventual; Owner: -
--

CREATE SEQUENCE eventual.documento_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 4390 (class 0 OID 0)
-- Dependencies: 438
-- Name: documento_id_seq; Type: SEQUENCE OWNED BY; Schema: eventual; Owner: -
--

ALTER SEQUENCE eventual.documento_id_seq OWNED BY eventual.documento.id;


--
-- TOC entry 447 (class 1259 OID 800210)
-- Name: documento_motorista; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.documento_motorista (
    id integer NOT NULL,
    motorista_id integer NOT NULL
);


--
-- TOC entry 446 (class 1259 OID 800209)
-- Name: documento_motorista_id_seq; Type: SEQUENCE; Schema: eventual; Owner: -
--

CREATE SEQUENCE eventual.documento_motorista_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 4391 (class 0 OID 0)
-- Dependencies: 446
-- Name: documento_motorista_id_seq; Type: SEQUENCE OWNED BY; Schema: eventual; Owner: -
--

ALTER SEQUENCE eventual.documento_motorista_id_seq OWNED BY eventual.documento_motorista.id;


--
-- TOC entry 437 (class 1259 OID 800002)
-- Name: documento_tipo; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.documento_tipo (
    nome text NOT NULL,
    descricao text,
    obrigatorio boolean DEFAULT false NOT NULL
);


--
-- TOC entry 443 (class 1259 OID 800073)
-- Name: documento_tipo_vinculo; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.documento_tipo_vinculo (
    tipo_nome text NOT NULL,
    entidade_tipo text NOT NULL,
    CONSTRAINT documento_tipo_permissao_entidade_tipo_check CHECK ((entidade_tipo = ANY (ARRAY['EMPRESA'::text, 'VEICULO'::text, 'MOTORISTA'::text])))
);


--
-- TOC entry 441 (class 1259 OID 800041)
-- Name: documento_usuario; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.documento_usuario (
    id integer NOT NULL,
    usuario_id integer NOT NULL
);


--
-- TOC entry 442 (class 1259 OID 800056)
-- Name: documento_veiculo; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.documento_veiculo (
    id integer NOT NULL,
    veiculo_placa text NOT NULL
);


--
-- TOC entry 453 (class 1259 OID 808150)
-- Name: documento_viagem; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.documento_viagem (
    id integer NOT NULL,
    viagem_id integer NOT NULL
);


--
-- TOC entry 459 (class 1259 OID 816815)
-- Name: fluxo_pendencia; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.fluxo_pendencia (
    id integer NOT NULL,
    criado_em timestamp without time zone DEFAULT now() NOT NULL,
    entidade_tipo text NOT NULL,
    entidade_id text NOT NULL,
    status text NOT NULL,
    analista text,
    motivo text,
    dados_propostos jsonb
);


--
-- TOC entry 458 (class 1259 OID 816814)
-- Name: fluxo_pendencia_id_seq; Type: SEQUENCE; Schema: eventual; Owner: -
--

CREATE SEQUENCE eventual.fluxo_pendencia_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 4392 (class 0 OID 0)
-- Dependencies: 458
-- Name: fluxo_pendencia_id_seq; Type: SEQUENCE OWNED BY; Schema: eventual; Owner: -
--

ALTER SEQUENCE eventual.fluxo_pendencia_id_seq OWNED BY eventual.fluxo_pendencia.id;


--
-- TOC entry 445 (class 1259 OID 800195)
-- Name: motorista; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.motorista (
    id integer NOT NULL,
    empresa_cnpj text NOT NULL,
    cpf text NOT NULL,
    cnh text NOT NULL,
    email text,
    nome text,
    criado_em timestamp without time zone DEFAULT now(),
    eventual_status text
);


--
-- TOC entry 444 (class 1259 OID 800194)
-- Name: motorista_id_seq; Type: SEQUENCE; Schema: eventual; Owner: -
--

CREATE SEQUENCE eventual.motorista_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 4393 (class 0 OID 0)
-- Dependencies: 444
-- Name: motorista_id_seq; Type: SEQUENCE OWNED BY; Schema: eventual; Owner: -
--

ALTER SEQUENCE eventual.motorista_id_seq OWNED BY eventual.motorista.id;


--
-- TOC entry 452 (class 1259 OID 808120)
-- Name: passageiro; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.passageiro (
    id integer NOT NULL,
    viagem_id integer NOT NULL,
    nome text NOT NULL,
    cpf text NOT NULL
);


--
-- TOC entry 451 (class 1259 OID 808119)
-- Name: passageiro_id_seq; Type: SEQUENCE; Schema: eventual; Owner: -
--

CREATE SEQUENCE eventual.passageiro_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 4394 (class 0 OID 0)
-- Dependencies: 451
-- Name: passageiro_id_seq; Type: SEQUENCE OWNED BY; Schema: eventual; Owner: -
--

ALTER SEQUENCE eventual.passageiro_id_seq OWNED BY eventual.passageiro.id;


--
-- TOC entry 456 (class 1259 OID 816800)
-- Name: status_pendencia; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.status_pendencia (
    status text NOT NULL,
    nome text NOT NULL
);


--
-- TOC entry 457 (class 1259 OID 816807)
-- Name: tipo_entidade_pendencia; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.tipo_entidade_pendencia (
    tipo text NOT NULL,
    descricao text NOT NULL
);


--
-- TOC entry 460 (class 1259 OID 816847)
-- Name: v_pendencia_atual; Type: VIEW; Schema: eventual; Owner: -
--

CREATE VIEW eventual.v_pendencia_atual AS
 SELECT DISTINCT ON (fluxo_pendencia.entidade_tipo, fluxo_pendencia.entidade_id) fluxo_pendencia.id,
    fluxo_pendencia.entidade_tipo,
    fluxo_pendencia.entidade_id,
    fluxo_pendencia.status,
    fluxo_pendencia.analista,
    fluxo_pendencia.criado_em,
    fluxo_pendencia.motivo,
    fluxo_pendencia.dados_propostos
   FROM eventual.fluxo_pendencia
  ORDER BY fluxo_pendencia.entidade_tipo, fluxo_pendencia.entidade_id, fluxo_pendencia.criado_em DESC;


--
-- TOC entry 450 (class 1259 OID 808062)
-- Name: viagem; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.viagem (
    id integer NOT NULL,
    nome_contratante text NOT NULL,
    cpf_cnpj_contratante text NOT NULL,
    regiao_codigo text NOT NULL,
    municipio_origem text NOT NULL,
    municipio_destino text NOT NULL,
    ida_em timestamp without time zone NOT NULL,
    volta_em timestamp without time zone NOT NULL,
    viagem_tipo text NOT NULL,
    veiculo_placa text NOT NULL,
    motorista_id integer NOT NULL,
    motorista_aux_id integer,
    descricao text,
    empresa_cnpj text NOT NULL,
    valor numeric(12,2) NOT NULL,
    pago boolean DEFAULT false NOT NULL
);


--
-- TOC entry 449 (class 1259 OID 808061)
-- Name: viagem_id_seq; Type: SEQUENCE; Schema: eventual; Owner: -
--

CREATE SEQUENCE eventual.viagem_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 4395 (class 0 OID 0)
-- Dependencies: 449
-- Name: viagem_id_seq; Type: SEQUENCE OWNED BY; Schema: eventual; Owner: -
--

ALTER SEQUENCE eventual.viagem_id_seq OWNED BY eventual.viagem.id;


--
-- TOC entry 448 (class 1259 OID 808001)
-- Name: viagem_tipo; Type: TABLE; Schema: eventual; Owner: -
--

CREATE TABLE eventual.viagem_tipo (
    nome text NOT NULL
);


--
-- TOC entry 434 (class 1259 OID 799650)
-- Name: papel; Type: TABLE; Schema: web; Owner: -
--

CREATE TABLE web.papel (
    nome text NOT NULL
);


--
-- TOC entry 455 (class 1259 OID 816620)
-- Name: token_validacao_email; Type: TABLE; Schema: web; Owner: -
--

CREATE TABLE web.token_validacao_email (
    id integer NOT NULL,
    usuario_id integer NOT NULL,
    token text NOT NULL,
    criado_em timestamp without time zone DEFAULT now() NOT NULL,
    expira_em timestamp without time zone NOT NULL
);


--
-- TOC entry 454 (class 1259 OID 816619)
-- Name: token_validacao_email_id_seq; Type: SEQUENCE; Schema: web; Owner: -
--

CREATE SEQUENCE web.token_validacao_email_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 4396 (class 0 OID 0)
-- Dependencies: 454
-- Name: token_validacao_email_id_seq; Type: SEQUENCE OWNED BY; Schema: web; Owner: -
--

ALTER SEQUENCE web.token_validacao_email_id_seq OWNED BY web.token_validacao_email.id;


--
-- TOC entry 436 (class 1259 OID 799667)
-- Name: usuario; Type: TABLE; Schema: web; Owner: -
--

CREATE TABLE web.usuario (
    id integer NOT NULL,
    papel_nome text NOT NULL,
    email text NOT NULL,
    nome text NOT NULL,
    cpf text,
    data_nascimento date,
    telefone text,
    senha text,
    empresa_cnpj text,
    criado_em timestamp without time zone DEFAULT now(),
    atualizado_em timestamp without time zone DEFAULT now(),
    ativo boolean DEFAULT true NOT NULL,
    email_validado boolean DEFAULT false NOT NULL
);


--
-- TOC entry 435 (class 1259 OID 799666)
-- Name: usuario_id_seq; Type: SEQUENCE; Schema: web; Owner: -
--

CREATE SEQUENCE web.usuario_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- TOC entry 4397 (class 0 OID 0)
-- Dependencies: 435
-- Name: usuario_id_seq; Type: SEQUENCE OWNED BY; Schema: web; Owner: -
--

ALTER SEQUENCE web.usuario_id_seq OWNED BY web.usuario.id;


--
-- TOC entry 4074 (class 2604 OID 800013)
-- Name: documento id; Type: DEFAULT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento ALTER COLUMN id SET DEFAULT nextval('eventual.documento_id_seq'::regclass);


--
-- TOC entry 4080 (class 2604 OID 800213)
-- Name: documento_motorista id; Type: DEFAULT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_motorista ALTER COLUMN id SET DEFAULT nextval('eventual.documento_motorista_id_seq'::regclass);


--
-- TOC entry 4086 (class 2604 OID 816818)
-- Name: fluxo_pendencia id; Type: DEFAULT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.fluxo_pendencia ALTER COLUMN id SET DEFAULT nextval('eventual.fluxo_pendencia_id_seq'::regclass);


--
-- TOC entry 4078 (class 2604 OID 800198)
-- Name: motorista id; Type: DEFAULT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.motorista ALTER COLUMN id SET DEFAULT nextval('eventual.motorista_id_seq'::regclass);


--
-- TOC entry 4083 (class 2604 OID 808123)
-- Name: passageiro id; Type: DEFAULT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.passageiro ALTER COLUMN id SET DEFAULT nextval('eventual.passageiro_id_seq'::regclass);


--
-- TOC entry 4081 (class 2604 OID 808065)
-- Name: viagem id; Type: DEFAULT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem ALTER COLUMN id SET DEFAULT nextval('eventual.viagem_id_seq'::regclass);


--
-- TOC entry 4084 (class 2604 OID 816623)
-- Name: token_validacao_email id; Type: DEFAULT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.token_validacao_email ALTER COLUMN id SET DEFAULT nextval('web.token_validacao_email_id_seq'::regclass);


--
-- TOC entry 4068 (class 2604 OID 799670)
-- Name: usuario id; Type: DEFAULT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.usuario ALTER COLUMN id SET DEFAULT nextval('web.usuario_id_seq'::regclass);


--
-- TOC entry 4364 (class 0 OID 800010)
-- Dependencies: 439
-- Data for Name: documento; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.documento (id, documento_tipo_nome, tamanho, hash, data_upload, validade, fluxo_pendencia_id, aprovado_em, conteudo, nome_arquivo, content_type) FROM stdin;
\.


--
-- TOC entry 4365 (class 0 OID 800024)
-- Dependencies: 440
-- Data for Name: documento_empresa; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.documento_empresa (id, empresa_cnpj) FROM stdin;
\.


--
-- TOC entry 4372 (class 0 OID 800210)
-- Dependencies: 447
-- Data for Name: documento_motorista; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.documento_motorista (id, motorista_id) FROM stdin;
\.


--
-- TOC entry 4362 (class 0 OID 800002)
-- Dependencies: 437
-- Data for Name: documento_tipo; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.documento_tipo (nome, descricao, obrigatorio) FROM stdin;
CARTAO_CNPJ	Cartão CNPJ	t
PROCURACAO	Procuração	f
NOTA_FISCAL	Nota Fiscal	f
CNAE_FISCAL	CNAE Fiscal	f
ALVARA	Alvará	f
LAUDO_INSPECAO	Laudo Inspeção	f
APOLICE_SEGURO	Apólice Seguro	f
COMPROVANTE_PAGAMENTO	Comprovante Pagamento	f
CONTRATO_SOCIAL	Contrato Social da Empresa	f
IDENTIDADE_SOCIO	Documento de Identidade do Sócio	f
CRLV	Certificado de Registro e Licenciamento de Veículo	t
CNH	Carteira Nacional de Habilitação	t
\.


--
-- TOC entry 4368 (class 0 OID 800073)
-- Dependencies: 443
-- Data for Name: documento_tipo_vinculo; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.documento_tipo_vinculo (tipo_nome, entidade_tipo) FROM stdin;
CARTAO_CNPJ	EMPRESA
CONTRATO_SOCIAL	EMPRESA
IDENTIDADE_SOCIO	EMPRESA
ALVARA	EMPRESA
CRLV	VEICULO
LAUDO_INSPECAO	VEICULO
APOLICE_SEGURO	VEICULO
CNH	MOTORISTA
\.


--
-- TOC entry 4366 (class 0 OID 800041)
-- Dependencies: 441
-- Data for Name: documento_usuario; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.documento_usuario (id, usuario_id) FROM stdin;
\.


--
-- TOC entry 4367 (class 0 OID 800056)
-- Dependencies: 442
-- Data for Name: documento_veiculo; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.documento_veiculo (id, veiculo_placa) FROM stdin;
\.


--
-- TOC entry 4378 (class 0 OID 808150)
-- Dependencies: 453
-- Data for Name: documento_viagem; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.documento_viagem (id, viagem_id) FROM stdin;
\.


--
-- TOC entry 4384 (class 0 OID 816815)
-- Dependencies: 459
-- Data for Name: fluxo_pendencia; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.fluxo_pendencia (id, criado_em, entidade_tipo, entidade_id, status, analista, motivo, dados_propostos) FROM stdin;
186	2026-03-10 15:06:40.778375	VEICULO	EEE3333	AGUARDANDO_ANALISE	\N	\N	{"Placa": "EEE3333", "Modelo": "modelito humilde", "Renavan": null, "ModeloAno": null, "ChassiNumero": null, "AnoFabricacao": null, "NumeroLugares": null, "PotenciaMotor": null, "VeiculoCombustivelNome": "DIESEL"}
187	2026-03-10 15:32:01.390034	VEICULO	EEE3333	EM_ANALISE	ana@ana	\N	{"Placa": "EEE3333", "Modelo": "modelito humilde", "Renavan": null, "ModeloAno": null, "ChassiNumero": null, "AnoFabricacao": null, "NumeroLugares": null, "PotenciaMotor": null, "VeiculoCombustivelNome": "DIESEL"}
188	2026-03-10 15:32:02.099142	VEICULO	EEE3333	APROVADO	ana@ana	\N	{"Placa": "EEE3333", "Modelo": "modelito humilde", "Renavan": null, "ModeloAno": null, "ChassiNumero": null, "AnoFabricacao": null, "NumeroLugares": null, "PotenciaMotor": null, "VeiculoCombustivelNome": "DIESEL"}
189	2026-03-10 15:33:11.339518	MOTORISTA	3	AGUARDANDO_ANALISE	\N	\N	{"Id": 3, "Cnh": "4984938778", "Cpf": "11111111111", "Nome": "josé antunes gabarito HONESTO e de fé", "Email": null}
190	2026-03-10 15:33:15.862612	MOTORISTA	3	EM_ANALISE	ana@ana	\N	{"Id": 3, "Cnh": "4984938778", "Cpf": "11111111111", "Nome": "josé antunes gabarito HONESTO e de fé", "Email": null}
191	2026-03-10 15:33:16.46168	MOTORISTA	3	APROVADO	ana@ana	\N	{"Id": 3, "Cnh": "4984938778", "Cpf": "11111111111", "Nome": "josé antunes gabarito HONESTO e de fé", "Email": null}
194	2026-03-12 12:52:49.328347	EMPRESA	3333333331313	AGUARDANDO_ANALISE	\N	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
195	2026-03-12 12:58:18.023128	EMPRESA	3333333331313	EM_ANALISE	ana@ana	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
196	2026-03-12 12:58:19.269183	EMPRESA	3333333331313	APROVADO	ana@ana	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
197	2026-03-12 13:04:49.475424	EMPRESA	3333333331313	AGUARDANDO_ANALISE	\N	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
198	2026-03-12 13:05:01.219623	EMPRESA	3333333331313	EM_ANALISE	ana@ana	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
199	2026-03-12 13:05:04.38272	EMPRESA	3333333331313	APROVADO	ana@ana	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
200	2026-03-12 13:11:19.635384	EMPRESA	3333333331313	AGUARDANDO_ANALISE	\N	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
201	2026-03-12 13:11:26.978023	EMPRESA	3333333331313	EM_ANALISE	ana@ana	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
202	2026-03-12 13:11:34.603673	EMPRESA	3333333331313	REJEITADO	ana@ana	no way rose	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
203	2026-03-12 13:11:47.846066	EMPRESA	3333333331313	AGUARDANDO_ANALISE	\N	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
204	2026-03-12 13:12:02.62522	EMPRESA	3333333331313	EM_ANALISE	ana@ana	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
205	2026-03-12 13:19:07.860746	EMPRESA	3333333331313	APROVADO	ana@ana	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
206	2026-03-12 13:35:31.976837	EMPRESA	3333333331313	AGUARDANDO_ANALISE	\N	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
207	2026-03-12 13:35:57.44659	EMPRESA	3333333331313	EM_ANALISE	ana@ana	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA BABADUKE MAS DE LUZ JUROOOOOOOOOO", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
208	2026-03-20 08:32:55.805396	EMPRESA	3333333331313	AGUARDANDO_ANALISE	\N	Desatribuição forçada por Administrador.	\N
209	2026-03-20 08:33:01.643341	EMPRESA	3333333331313	EM_ANALISE	ana@ana	\N	\N
210	2026-03-20 08:33:20.968915	EMPRESA	3333333331313	AGUARDANDO_ANALISE	\N	Desatribuição forçada por Administrador.	\N
211	2026-03-20 08:35:02.498892	EMPRESA	3333333331313	EM_ANALISE	ana@ana	\N	\N
212	2026-03-20 08:35:15.638737	EMPRESA	3333333331313	REJEITADO	ana@ana	SEM CHNACES!!!!!!!	\N
213	2026-03-20 08:42:28.27678	VEICULO	EEE3333	AGUARDANDO_ANALISE	\N	\N	{"Placa": "EEE3333", "Modelo": "modelito humilde gente!!!!!", "Renavan": null, "ModeloAno": null, "ChassiNumero": null, "AnoFabricacao": null, "NumeroLugares": null, "PotenciaMotor": null, "VeiculoCombustivelNome": "DIESEL"}
214	2026-03-20 08:42:44.914076	VEICULO	EEE3333	EM_ANALISE	ana@ana	\N	{"Placa": "EEE3333", "Modelo": "modelito humilde gente!!!!!", "Renavan": null, "ModeloAno": null, "ChassiNumero": null, "AnoFabricacao": null, "NumeroLugares": null, "PotenciaMotor": null, "VeiculoCombustivelNome": "DIESEL"}
215	2026-03-20 12:02:14.883454	VEICULO	EEE3333	REJEITADO	ana@ana	uepa	{"Placa": "EEE3333", "Modelo": "modelito humilde gente!!!!!", "Renavan": null, "ModeloAno": null, "ChassiNumero": null, "AnoFabricacao": null, "NumeroLugares": null, "PotenciaMotor": null, "VeiculoCombustivelNome": "DIESEL"}
216	2026-03-24 11:54:16.054984	MOTORISTA	3	AGUARDANDO_ANALISE	\N	\N	{"Id": 3, "Cnh": "4984938778", "Cpf": "11111111111", "Nome": "josé antunes gabarito HONESTO e de fé", "Email": null}
217	2026-03-24 11:54:51.161549	MOTORISTA	3	EM_ANALISE	ana@ana	\N	{"Id": 3, "Cnh": "4984938778", "Cpf": "11111111111", "Nome": "josé antunes gabarito HONESTO e de fé", "Email": null}
218	2026-03-24 11:54:59.08779	MOTORISTA	3	APROVADO	ana@ana	\N	{"Id": 3, "Cnh": "4984938778", "Cpf": "11111111111", "Nome": "josé antunes gabarito HONESTO e de fé", "Email": null}
219	2026-03-24 11:55:24.176134	MOTORISTA	5	AGUARDANDO_ANALISE	\N	\N	\N
220	2026-03-24 11:55:36.665313	MOTORISTA	5	EM_ANALISE	ana@ana	\N	\N
221	2026-03-24 11:55:44.759413	MOTORISTA	5	REJEITADO	ana@ana	nao nao que isso????	\N
222	2026-03-24 12:43:04.250884	VEICULO	EEE3333	AGUARDANDO_ANALISE	\N	\N	{"Placa": "EEE3333", "Modelo": "modelito humilde gente!!!!!", "Renavan": null, "ModeloAno": null, "ChassiNumero": null, "AnoFabricacao": null, "NumeroLugares": null, "PotenciaMotor": null, "VeiculoCombustivelNome": "DIESEL"}
223	2026-03-24 12:51:30.039107	MOTORISTA	5	AGUARDANDO_ANALISE	\N	\N	{"Id": 5, "Cnh": "65656556", "Cpf": "65656565655", "Nome": "joão válido siiim", "Email": null}
224	2026-03-24 12:55:03.494141	EMPRESA	3333333331313	AGUARDANDO_ANALISE	\N	\N	{"Cep": null, "Cnpj": "3333333331313", "Nome": "EMPRESA marmaduke?", "Email": "NET@NET", "Bairro": null, "Cidade": null, "Estado": null, "Endereco": null, "Telefone": "5555555555558", "NomeFantasia": "FANTASIA", "EnderecoNumero": null, "EnderecoComplemento": null}
\.


--
-- TOC entry 4370 (class 0 OID 800195)
-- Dependencies: 445
-- Data for Name: motorista; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.motorista (id, empresa_cnpj, cpf, cnh, email, nome, criado_em, eventual_status) FROM stdin;
2	3333333331313	12111111111	7777777777	joseh@lala	joseh	2026-03-02 13:47:12.48619	AGUARDANDO_ANALISE
4	3333333331313	99999999988	9999999898989	\N	manuelito boas-novas	2026-03-02 22:59:22.068006	REJEITADO
1	03929471000177	111111	1111111	dggffjkfj@cjhcf	motoristinha	2025-11-05 18:35:24.690815	REJEITADO
3	3333333331313	11111111111	4984938778	\N	josé antunes gabarito HONESTO e de fé	2026-03-02 15:49:20.948826	AGUARDANDO_ANALISE
5	3333333331313	65656565655	65656556	\N	joão válido	2026-03-05 19:22:32.529586	AGUARDANDO_ANALISE
\.


--
-- TOC entry 4377 (class 0 OID 808120)
-- Dependencies: 452
-- Data for Name: passageiro; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.passageiro (id, viagem_id, nome, cpf) FROM stdin;
1	2	454545	45454545
2	3	222222	2222222
3	4	454545	45454545
4	5	222222	2222222
5	6	33333	3333333
6	7	jukiha	2039399
7	8	jack	232323
8	8	john	24224224
9	9	jane	43434
10	10	ssssssss	2121212
\.


--
-- TOC entry 4381 (class 0 OID 816800)
-- Dependencies: 456
-- Data for Name: status_pendencia; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.status_pendencia (status, nome) FROM stdin;
AGUARDANDO_ANALISE	Aguardando Análise
EM_ANALISE	Em Análise
APROVADO	Aprovado
REJEITADO	Rejeitado
\.


--
-- TOC entry 4382 (class 0 OID 816807)
-- Dependencies: 457
-- Data for Name: tipo_entidade_pendencia; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.tipo_entidade_pendencia (tipo, descricao) FROM stdin;
EMPRESA	Empresa
VEICULO	Veículo
MOTORISTA	Motorista
\.


--
-- TOC entry 4375 (class 0 OID 808062)
-- Dependencies: 450
-- Data for Name: viagem; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.viagem (id, nome_contratante, cpf_cnpj_contratante, regiao_codigo, municipio_origem, municipio_destino, ida_em, volta_em, viagem_tipo, veiculo_placa, motorista_id, motorista_aux_id, descricao, empresa_cnpj, valor, pago) FROM stdin;
2	2323	2323	AULINOR	PALMARES DO SUL	OSÓRIO	2026-03-11 08:00:00	2026-03-28 18:00:00	Congressos	EEE3333	3	\N	2323	3333333331313	3434.00	f
3	21	2222222	AULINOR	OSÓRIO	TRAMANDAÍ	2026-03-11 08:00:00	2026-04-25 18:00:00	Encontros Religiosos	EEE3333	3	\N	\N	3333333331313	3535.00	f
4	2323	2323	AULINOR	PALMARES DO SUL	OSÓRIO	2026-03-11 08:00:00	2026-03-28 18:00:00	Congressos	EEE3333	3	\N	2323	3333333331313	366.00	f
5	21	2222222	AULINOR	OSÓRIO	TRAMANDAÍ	2026-03-11 08:00:00	2026-04-25 18:00:00	Encontros Religiosos	EEE3333	3	\N	\N	3333333331313	535.00	f
6	33333	333333333	RMSG	PINTO BANDEIRA	PINTO BANDEIRA	2026-03-11 08:00:00	2026-03-28 18:00:00	Encontros Religiosos	EEE3333	3	\N	33333	3333333331313	3434.00	f
7	ggggggggg	54545	AULINOR	AULINOR	CAPIVARI DO SUL	2026-03-12 08:00:00	2026-04-30 18:00:00	Encontros Religiosos	EEE3333	3	\N	4545	3333333331313	53535.00	t
8	bill	349834983	RMPA	ESTEIO	GLORINHA	2026-03-12 08:00:00	2026-03-26 18:00:00	Encontros Esportivos	EEE3333	3	\N	\N	3333333331313	385.50	f
9	jack	8349384	RMPA	GRAVATAÍ	GLORINHA	2026-03-12 08:00:00	2026-03-20 18:00:00	Eventos	EEE3333	3	\N	\N	3333333331313	385.50	t
10	eeeeeeee	21121212	RMPA	GLORINHA	GUAÍBA	2026-03-13 08:00:00	2026-03-27 18:00:00	Encontros Esportivos	EEE3333	3	\N	\N	3333333331313	385.50	t
\.


--
-- TOC entry 4373 (class 0 OID 808001)
-- Dependencies: 448
-- Data for Name: viagem_tipo; Type: TABLE DATA; Schema: eventual; Owner: -
--

COPY eventual.viagem_tipo (nome) FROM stdin;
Passeios Turísticos
Excursões
Eventos
Congressos
Shows
Festas
Encontros Religiosos
Encontros Esportivos
Outros
\.


--
-- TOC entry 4359 (class 0 OID 799650)
-- Dependencies: 434
-- Data for Name: papel; Type: TABLE DATA; Schema: web; Owner: -
--

COPY web.papel (nome) FROM stdin;
EMPRESA
USUARIO_EMPRESA
ANALISTA
ADMIN
\.


--
-- TOC entry 4380 (class 0 OID 816620)
-- Dependencies: 455
-- Data for Name: token_validacao_email; Type: TABLE DATA; Schema: web; Owner: -
--

COPY web.token_validacao_email (id, usuario_id, token, criado_em, expira_em) FROM stdin;
48	93	D46415D771B73465EB24F620826065631F11FCB6FD0D27935C59E389792196B5	2026-03-19 09:20:05.248487	2026-03-20 09:20:05.248559
30	64	MFqsq3oDjh_wOy2WGLuZPa5uashF7D75LWgCG6gx4u0	2026-01-09 11:52:09.815353	2026-01-10 11:52:09.815356
35	80	9E119764762B8BBAC9D13394A687AD61C528F8F370C3EFC016E33D665299A40A	2026-03-08 16:27:59.483559	2026-03-09 16:27:59.483606
37	82	3450E6B8E7E60AAEF870E24AC3095991F483F3FD67D6B1EBA6CD9D7813A7CA6D	2026-03-08 16:36:53.048076	2026-03-09 16:36:53.048076
38	83	A6F8AAFA1FEB4763BE4CA1EE34023C54C98BBA57AEAFD7F7C6F6BDD31DDD6457	2026-03-08 16:39:52.934511	2026-03-09 16:39:52.934553
39	84	851248A8DB3088B7353F8E9970C983685AD363D667B5CE1CF5C8B0404F6C5CAA	2026-03-08 16:42:57.420063	2026-03-09 16:42:57.420064
40	85	D09510C56CFAF68BE27E56F4EBD44240F213F8865E3665B35593D337365B7EB5	2026-03-08 16:47:45.123428	2026-03-09 16:47:45.123428
41	86	45F3E2042E2D6EAF6E921E5F1E1F9F8C28DF96E584C93C52206B0D61F2FB9814	2026-03-08 16:48:47.408562	2026-03-09 16:48:47.408606
42	87	2AA6C7D902BBD1A3A7FE1DFC436A8425CEE0CDC052AF7CECDB80BA0B15D283BA	2026-03-08 16:49:41.320905	2026-03-09 16:49:41.320949
43	88	EB103292AD101A11FC7806B50126FE967243D688A04924B033D7C740AB041ECE	2026-03-08 16:51:11.646282	2026-03-09 16:51:11.646321
44	89	02D20EA910909E25E0DC5B44D4A55D61F6FF97FED675CA926BEF60DB9E9B42C1	2026-03-08 16:54:30.550162	2026-03-09 16:54:30.550192
\.


--
-- TOC entry 4361 (class 0 OID 799667)
-- Dependencies: 436
-- Data for Name: usuario; Type: TABLE DATA; Schema: web; Owner: -
--

COPY web.usuario (id, papel_nome, email, nome, cpf, data_nascimento, telefone, senha, empresa_cnpj, criado_em, atualizado_em, ativo, email_validado) FROM stdin;
64	EMPRESA	skcjwij@iksjncfwic	76777	\N	\N	\N	\N	86464646464	2026-01-09 08:52:09.778803	2026-01-09 08:52:09.778803	t	f
90	EMPRESA	marcelohz@gmail.com	Nova Empresa	\N	\N	\N	AQAAAAIAAYagAAAAECX3INmg4b0Vm2afeky/G+Blai2r2c+cab7SYhNmBNFb31jEmwvPRgF24qPjLS4IIw==	090900909090	2026-03-10 15:25:17.984868	2026-03-10 15:25:18.058978	t	t
74	ANALISTA	ana@ana	Ana Analista	\N	\N	\N	AQAAAAIAAYagAAAAEKNZvVBcnme933eYWGEONHGwFU055WdGF0Xcf+0ti8yhMnkR95P3hvfLkGHlbiXWBg==	\N	2026-03-03 00:44:52.269638	2026-03-02 21:44:52.871984	t	t
80	EMPRESA	tt@tt	Nova Empresa	\N	\N	\N	fc9e5128-343c-439a-8034-eb53535c7046	199191919119	2026-03-08 16:27:59.2451	2026-03-08 16:27:59.550483	t	f
85	EMPRESA	papa@ppa	Nova Empresa	\N	\N	\N	d62339b7-e28d-4c91-a202-117d2a41e552	6765444444444	2026-03-08 16:47:45.04466	2026-03-08 16:47:45.526868	t	f
88	EMPRESA	ppppppp@ssssssssssss	Nova Empresa	\N	\N	\N	aca0ccd2-5823-4872-ab8c-d56908581157	324334343434	2026-03-08 16:51:11.505104	2026-03-08 16:51:11.890412	t	f
92	USUARIO_EMPRESA	marcelohz+9998@gmail.com	Levi Raspa Moeda	666	\N	\N	AQAAAAIAAYagAAAAEBiZyzyWv9YLCjbmw9fu9Knec5mmBAOhhns74w1fMA9msERwc+XV92Ka61ytWgBi2g==	3333333331313	2026-03-13 09:15:31.386576	2026-03-13 09:15:31.455486	f	t
93	EMPRESA	marcelohz+8989@gmail.com	Nova Empresa	\N	\N	\N	f7c6e33a-beb9-446f-a3aa-6422833e1c5f	39283928392893	2026-03-19 09:20:05.143017	2026-03-19 09:20:04.920756	t	f
3	EMPRESA	oi@oi	oioi	\N	\N	\N	scrypt:32768:8:1$U6zR2voA2GjhqaaV$f10a24823f57b098ddbc2b949a33c05c5476ce4f57b9d1e122238f2962249271d4b5933cf8b1e032ef0f1ed8d154bd59000e5862d3c08f7c79f94d92c671cbc9	123456	2025-10-24 10:39:53.851594	2025-10-24 10:39:53.851594	t	t
4	EMPRESA	dois@dois	razao 2	\N	\N	\N	scrypt:32768:8:1$BLZQrce2GnvQuDY2$fc927c18ce988e1531570179670b2495ad1348d69999bac62b5e12f09873006ea556a8a2cc96ffd27b7b62697380c828b78cb02410b8bc96db97678f8a935f50	222222222222222222	2025-10-28 08:56:20.623233	2025-10-28 08:56:20.623233	t	t
5	EMPRESA	tres@tres	raz 3	\N	\N	\N	scrypt:32768:8:1$HGGCkZufzypUhTHu$f5b594b580b9f676b36aef235224fd62fa5c99a6f43dc562651585b8ed3b33c016f3b94ac537f100596cc5be4f6fef1c03433c7b52467569bd7e127406840a0a	333333333	2025-10-28 10:07:55.869278	2025-10-28 10:07:55.869278	t	t
7	USUARIO_EMPRESA	lalalal@dlod	noooooome	1111111111	2002-02-02	11111	scrypt:32768:8:1$HCHfO5tCGXU0s405$81787e588a4f8f08b8f777efe0e209d1922a246f468c79aad248e8de6638badac81b335c5ee2c083d9618cfa9b42ff6d814e957fa6f0739d02d629fa9839aa91	\N	2025-10-29 10:52:17.430819	2025-10-29 10:52:17.430819	t	t
65	EMPRESA	lalala@clclwdlc.com	88888888898989	\N	\N	\N	\N	88888898989888	2026-01-09 08:56:00.565115	2026-01-09 08:56:12.176372	t	t
68	EMPRESA	net@net	net	\N	\N	\N	AQAAAAIAAYagAAAAEEAFwXnSqxkorD1NYBRAjNmzPwvRLDtJpMSIe/WW+p/zrnDefXZ6MUJuwKck3ylGwQ==	3333333331313	2026-03-02 13:07:06.525791	2026-03-02 13:07:06.525791	t	t
75	EMPRESA	lolo@lolo	nnnnn	\N	\N	\N	AQAAAAIAAYagAAAAEND5Fbq55Mohs9jMlFkt0GzUm/XgdD98iXq78YfMhd3DR6gpAypPuhiERZfcbyRZHw==	777777766666	2026-03-04 18:28:03.680776	2026-03-04 18:28:03.680776	t	t
78	EMPRESA	ss@ss	Nova Empresa	\N	\N	\N	master	141414141414	2026-03-08 17:56:40.563885	2026-03-08 14:56:40.721082	t	t
81	EMPRESA	tgtg@fgtf	Nova Empresa	\N	\N	\N	AQAAAAIAAYagAAAAELISO38mFw4yiyrRgOpHV9sh34hG8AfOTgyXuc5vuN5bvsg3cR6pnwdLYP3q6boNxg==	878778877887	2026-03-08 16:33:18.494516	2026-03-08 16:33:18.807478	t	t
82	EMPRESA	i@i	Nova Empresa	\N	\N	\N	4202af36-bb01-4508-b679-ece681a9c74d	9637868758676	2026-03-08 16:36:52.947936	2026-03-08 16:36:53.400892	t	f
86	EMPRESA	ssssssss@ssdssssssssssss	Nova Empresa	\N	\N	\N	cc850ce7-6a9f-4285-9f89-6fdda349f502	8484844848656	2026-03-08 16:48:47.235635	2026-03-08 16:48:47.626495	t	f
89	EMPRESA	dddddddddddd@kkkkkkkkkk	Nova Empresa	\N	\N	\N	a4e562bd-b6a0-4c29-b473-c9bbc4a6cc95	888878787878787	2026-03-08 16:54:30.414697	2026-03-08 16:54:30.778998	t	f
91	EMPRESA	marcelohz+1@gmail.com	Nova Empresa	\N	\N	\N	AQAAAAIAAYagAAAAEMiFej5BUKi7JToIMuN3tIxS3V4IwXn4/ZkrgF1XmmADSiryLNhcoggjzEZ/IrCQTA==	234298238293	2026-03-11 09:14:44.447334	2026-03-11 09:14:44.024898	t	t
31	EMPRESA	newempresa@example.com	Empresa Nova S.A.	\N	\N	\N	scrypt:32768:8:1$jYzkzWwh5abWqMxJ$c824a83363254fbb530ecdbff399aef86f1d28a30472ab3530be49e74c6ac6ba451e4318edb102d6797cbd50edc68fbd1a225ea684f38385932229b221e74477	11111111000199	2025-11-18 12:07:17.433554	2025-11-18 12:07:17.433554	f	t
24	EMPRESA	pend@pend	pendentihno	\N	\N	\N	scrypt:32768:8:1$PFTWPZahd0m7LPP0$45594b400dab4ab516a929a4c69d4bf49697b16256ec122a00f08fa2fafb362baee7036c8438b5e77693a4f79f1e517c8829f6288ad024b54579d717c582d6f9	33333333333333	2025-11-06 10:33:28.747254	2025-11-06 10:33:28.747254	t	t
14	EMPRESA	doc@doc	documentos	\N	\N	\N	scrypt:32768:8:1$Xri5ilm6piR7Qzqr$7ec26332ceaeb41e6f7fed87598d5d05ee82b02528e88c2d3e6e251c9b8365bce0a48adb47263e826f3ce575493546daac55d553616265411c7550f481360c92	6565655665574	2025-10-31 13:18:43.167498	2025-10-31 13:18:43.167498	t	t
20	USUARIO_EMPRESA	johnny@atered	johnny altered	23489843943	2025-11-04		scrypt:32768:8:1$VzCMS6eclI5kKf8O$ad3aa504b22903e9d8ac5fc93f3b3ab4adcd2e8c38cad6ae6678995e17a30d2dce4415cd9f6fde152c4384aad997c30ee71ac66f299125507db5a5ba4deae17e	03929471000177	2025-11-04 09:54:54.133977	2025-11-10 08:09:41.920885	f	t
32	EMPRESA	empresaexistente@example.com	Empresa Existente SA	\N	\N	\N	scrypt:32768:8:1$eokrCa8EXoZDbvAO$fb1972e8799420b201f91028dcb5d269d215cdc7e100ea49d4e935bff54d5d031e8c075e76a199097d080950066ebfbd9f8a7119a37c85715dd0ce55f8ba3da5	22222222000133	2025-11-18 12:07:26.546968	2025-11-18 12:07:26.546968	f	t
19	USUARIO_EMPRESA	manuelito@bang	manuelito bangbang	12121121212	2002-02-02	554545454	scrypt:32768:8:1$yvt3f0bTucquMOio$db8e803567b1dc293d5311c1f000f1ffb8c7a32f9fe952b950b46a8314e8ae456376d97e49670b8a0f8091a7c825eec329ae43a300bcf934433dd940598bc292	03929471000177	2025-11-04 09:45:15.597911	2025-11-04 12:11:02.397674	f	t
15	USUARIO_EMPRESA	novo@novo	mais um user	9999999999	2002-02-02	515151515	scrypt:32768:8:1$a8gGq3cn1CGlHpr2$b4c49787a8cfe2be686d06103b965a17bb21dfd0da91bdc79f4a458448ebc1d49f070038f54e3e100c1e8897661e8905f13e6448a49ce671f27ddbaf29dbd97b	03929471000177	2025-11-03 07:48:56.371969	2025-11-04 12:12:06.371379	f	t
18	USUARIO_EMPRESA	tana@jura	tanajnura	656565	2002-02-02	51515	scrypt:32768:8:1$F4USvrEPyLUCqUQ7$a9763d5693d820c1a96a212e5d28bde426abaebe5257f17b440ecdcd0de9846126eb5b7ddc6296926f9370f1d59018874dbe823ad795a38b368fbd0fcb206781	03929471000177	2025-11-03 08:13:40.099598	2025-11-04 11:46:47.506465	f	t
17	USUARIO_EMPRESA	cigana@cigana	cigana	23982983	2002-01-01	151515	scrypt:32768:8:1$CdKUKo9SRRuj9gwU$4c0ab4b60bf59d6f7ee89bed776e3bf14bfd08624f14d763a51f5283eac8849078c4dd987de511055b2b7c2f6b48591c5d87bd63db9b5b1ce26cee9f24e7ac4c	03929471000177	2025-11-03 07:57:47.187123	2025-11-04 11:46:49.345364	f	t
22	USUARIO_EMPRESA	ebilero@wasoyijhg	ebilemaero	8988888	2002-02-02	323232323	scrypt:32768:8:1$TD6gQ99jUSfUYor6$9f71bfec2ec8081b0bcffa4e99738a349f034a0a880bc9bc52a6fc79ef39db2e59bc3cbea30356807f7f7d1827263e7685f8931051838b69b42249b1482018d4	03929471000177	2025-11-04 11:47:44.326372	2025-11-04 13:09:36.775099	f	t
21	USUARIO_EMPRESA	capencio@klala	capencio orioundo	23293829382	2002-02-02	787787	scrypt:32768:8:1$pGqjGboZSN4Rxh9F$eb1cfc73c6593f6534ed8d89ecf14f47f635f30661ab37e412c59577e73b9eb0d37ae8b186e08b1ada88e1caec4a97596b794edbdac232e02e6a8e751562adaf	03929471000177	2025-11-04 11:42:17.912711	2025-11-04 11:42:38.65908	t	t
23	ANALISTA	amanda	amanda gomez		2002-02-02		scrypt:32768:8:1$vUTnyBOcclIzTdHJ$e4b7b578e2589c1cb45370f29087325862aab982966880e5c227132c8d95050c19aec10aa1975496f98bb072e1fdfda5aef534377a47636ea0fd3b6b32851cd7	03929471000177	2025-11-04 13:12:56.322542	2025-11-04 13:12:56.322542	t	t
10	USUARIO_EMPRESA	bilu@bilu	bilu	2262626262	2002-11-11	99999999999	scrypt:32768:8:1$gy6adl2GL0bqvfRd$f8f8018c64c041e23018f01ea92cd3c4163f40a61a3a262a393bdfd3f9f25df8049b70b71f6b65cb7dc1f38c8ec7a3c8a72f6e46a83df53faaa29c78793ef75a	03929471000177	2025-10-29 12:51:24.966618	2025-11-04 11:47:57.450654	f	t
16	USUARIO_EMPRESA	nasceu@para	nasci pra sqn	6666666666	2222-01-01	11515115	scrypt:32768:8:1$PyHTMD7RRD95bZ3T$d5a3b4da866e1aa4d15ac25a00bb43fe25c4a26c769ced9703d83525494799c9e4605149bab8c5b57e962ca7558355aaeb843a728f25e1d276de47c7700de760	03929471000177	2025-11-03 07:57:15.707232	2025-11-04 13:12:16.236831	f	t
30	EMPRESA	nove@nove		\N	\N	\N	scrypt:32768:8:1$bM7QseII5H8IT2qZ$e326cefe8c4a9431ce0f1d7ed1b2fc1ad1ec757e3d2591d5ffda13fed13c3cb1e29283f050acae846b1fe015c123aef9a2b897e07d82747eba9a22218c15236e	99999999999999	2025-11-18 08:35:31.471573	2025-11-18 08:35:31.471573	t	t
33	EMPRESA	existinguser@example.com	Empresa Duplicada SA	\N	\N	\N	scrypt:32768:8:1$GXcBLKZivv3AgToe$0c36a93662aeeb8bf71f9f5edca25b4a306f6cdf57b2b74c1aaed56fcde9fe06508df961ddd2f75317d5a89d307cc19df4e8297507c2a99cf34be3963d131e31	33333333000155	2025-11-18 12:07:43.705577	2025-11-18 12:07:43.705577	f	t
37	ANALISTA	anal@anal	anal	22222222223	2002-02-02	\N	scrypt:32768:8:1$vUTnyBOcclIzTdHJ$e4b7b578e2589c1cb45370f29087325862aab982966880e5c227132c8d95050c19aec10aa1975496f98bb072e1fdfda5aef534377a47636ea0fd3b6b32851cd7	\N	2025-11-17 14:00:55.295952	2025-11-17 14:00:55.295952	t	t
66	EMPRESA	lala@lala	TESTE	\N	\N	\N	scrypt:32768:8:1$j5ILc9Oz39TaMYiJ$8b70feda981a0f4ce1deca3a9d753f4c16309eb1a84e6429b3fa2900819883cd2f24cc2e504f6938b4ce50d584d9ff550a66be25db3bcd3b69211a793817739f	4545454545	2026-01-12 10:48:56.066904	2026-01-12 10:59:12.228464	t	t
38	EMPRESA	woo@woo	WOO HOOO	\N	\N	\N	scrypt:32768:8:1$XVz3fj6EKQa6z2ow$ab0e29e9043ad98bb3c514fc0753f60f8eb05d115a484a0ca0e86150e47fe049722d2dffc2fddc5bb8faa1492a7bdbc8d65ec699a66782f93c73280ad58d5e69	99999595959595	2025-11-24 09:54:16.488275	2025-11-24 09:54:16.488275	f	t
27	ANALISTA	anal2@anal2	anal	4466655555	2002-02-02	\N	scrypt:32768:8:1$vUTnyBOcclIzTdHJ$e4b7b578e2589c1cb45370f29087325862aab982966880e5c227132c8d95050c19aec10aa1975496f98bb072e1fdfda5aef534377a47636ea0fd3b6b32851cd7	\N	2025-11-17 14:00:55.295952	2025-11-17 14:00:55.295952	t	t
39	EMPRESA	wopkodowijeifeif.com	RTAZ	\N	\N	\N	scrypt:32768:8:1$VrWA3UIZSKdrXz7E$37063ac7be5047eb962eeb342d634e25c86e39c847e7eaf7b085144e26e6ca0b1661d9e7db7fbb3b54d13db7cdba7f549dc8c8cf7d512bcdf0acef4e695adeb8	11111111111121	2025-12-15 08:06:02.823267	2025-12-15 08:06:02.823267	f	t
42	EMPRESA	llllmdmmdmdmdm.com	POPOPO	\N	\N	\N	scrypt:32768:8:1$2LmqLeAsYGdOqEpa$b66b23f69f5121bda4763cfe1a9eeefab693ad558806556e2010872c81f3a9537a7be5aa32714b170897d2c2b998b1d43b2f970f9e0deefd1f98ddd33ea36144	888787878787887	2025-12-15 08:56:14.144486	2025-12-15 08:56:14.144486	f	t
73	ADMIN	adm@adm	joão administrador	\N	\N	\N	AQAAAAIAAYagAAAAEEAFwXnSqxkorD1NYBRAjNmzPwvRLDtJpMSIe/WW+p/zrnDefXZ6MUJuwKck3ylGwQ==	\N	2026-03-02 13:07:06.525791	2026-03-02 13:07:06.525791	t	t
79	EMPRESA	five@five	Nova Empresa	\N	\N	\N	AQAAAAIAAYagAAAAEBEG0X/iaPQeLnhmvD9mLSyvsgKyfuI4aFy/GhLJD4BKqIK9d/skvJccu7s+kcVF7g==	55555555555	2026-03-08 18:22:25.207144	2026-03-08 15:22:25.479319	t	t
83	EMPRESA	serio@serio	Nova Empresa	\N	\N	\N	5e6c2e3b-7e23-4c12-9703-47560ce14928	2222222222222	2026-03-08 16:39:52.801309	2026-03-08 16:39:53.178618	t	f
84	EMPRESA	aaaaaaaaaaaaaaaa@aaaaaaaaaaaaaaaa	Nova Empresa	\N	\N	\N	6e364a9a-d3cd-4e47-bbf2-6319948c095d	333333434344	2026-03-08 16:42:57.320658	2026-03-08 16:42:57.78705	t	f
87	EMPRESA	sdkqowdk@dldlod	Nova Empresa	\N	\N	\N	17205d01-1475-405c-b149-d6d5bda63171	2444444444444	2026-03-08 16:49:41.17483	2026-03-08 16:49:41.55356	t	f
\.


--
-- TOC entry 4398 (class 0 OID 0)
-- Dependencies: 438
-- Name: documento_id_seq; Type: SEQUENCE SET; Schema: eventual; Owner: -
--

SELECT pg_catalog.setval('eventual.documento_id_seq', 106, true);


--
-- TOC entry 4399 (class 0 OID 0)
-- Dependencies: 446
-- Name: documento_motorista_id_seq; Type: SEQUENCE SET; Schema: eventual; Owner: -
--

SELECT pg_catalog.setval('eventual.documento_motorista_id_seq', 1, false);


--
-- TOC entry 4400 (class 0 OID 0)
-- Dependencies: 458
-- Name: fluxo_pendencia_id_seq; Type: SEQUENCE SET; Schema: eventual; Owner: -
--

SELECT pg_catalog.setval('eventual.fluxo_pendencia_id_seq', 224, true);


--
-- TOC entry 4401 (class 0 OID 0)
-- Dependencies: 444
-- Name: motorista_id_seq; Type: SEQUENCE SET; Schema: eventual; Owner: -
--

SELECT pg_catalog.setval('eventual.motorista_id_seq', 6, true);


--
-- TOC entry 4402 (class 0 OID 0)
-- Dependencies: 451
-- Name: passageiro_id_seq; Type: SEQUENCE SET; Schema: eventual; Owner: -
--

SELECT pg_catalog.setval('eventual.passageiro_id_seq', 10, true);


--
-- TOC entry 4403 (class 0 OID 0)
-- Dependencies: 449
-- Name: viagem_id_seq; Type: SEQUENCE SET; Schema: eventual; Owner: -
--

SELECT pg_catalog.setval('eventual.viagem_id_seq', 10, true);


--
-- TOC entry 4404 (class 0 OID 0)
-- Dependencies: 454
-- Name: token_validacao_email_id_seq; Type: SEQUENCE SET; Schema: web; Owner: -
--

SELECT pg_catalog.setval('web.token_validacao_email_id_seq', 48, true);


--
-- TOC entry 4405 (class 0 OID 0)
-- Dependencies: 435
-- Name: usuario_id_seq; Type: SEQUENCE SET; Schema: web; Owner: -
--

SELECT pg_catalog.setval('web.usuario_id_seq', 93, true);


--
-- TOC entry 4106 (class 2606 OID 800030)
-- Name: documento_empresa documento_empresa_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_empresa
    ADD CONSTRAINT documento_empresa_pkey PRIMARY KEY (id);


--
-- TOC entry 4120 (class 2606 OID 800215)
-- Name: documento_motorista documento_motorista_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_motorista
    ADD CONSTRAINT documento_motorista_pkey PRIMARY KEY (id);


--
-- TOC entry 4102 (class 2606 OID 800018)
-- Name: documento documento_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento
    ADD CONSTRAINT documento_pkey PRIMARY KEY (id);


--
-- TOC entry 4115 (class 2606 OID 800080)
-- Name: documento_tipo_vinculo documento_tipo_permissao_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_tipo_vinculo
    ADD CONSTRAINT documento_tipo_permissao_pkey PRIMARY KEY (tipo_nome, entidade_tipo);


--
-- TOC entry 4100 (class 2606 OID 800008)
-- Name: documento_tipo documento_tipo_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_tipo
    ADD CONSTRAINT documento_tipo_pkey PRIMARY KEY (nome);


--
-- TOC entry 4109 (class 2606 OID 800045)
-- Name: documento_usuario documento_usuario_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_usuario
    ADD CONSTRAINT documento_usuario_pkey PRIMARY KEY (id);


--
-- TOC entry 4112 (class 2606 OID 800062)
-- Name: documento_veiculo documento_veiculo_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_veiculo
    ADD CONSTRAINT documento_veiculo_pkey PRIMARY KEY (id);


--
-- TOC entry 4135 (class 2606 OID 808154)
-- Name: documento_viagem documento_viagem_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_viagem
    ADD CONSTRAINT documento_viagem_pkey PRIMARY KEY (id);


--
-- TOC entry 4150 (class 2606 OID 816823)
-- Name: fluxo_pendencia fluxo_pendencia_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.fluxo_pendencia
    ADD CONSTRAINT fluxo_pendencia_pkey PRIMARY KEY (id);


--
-- TOC entry 4118 (class 2606 OID 800203)
-- Name: motorista motorista_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.motorista
    ADD CONSTRAINT motorista_pkey PRIMARY KEY (id);


--
-- TOC entry 4131 (class 2606 OID 808127)
-- Name: passageiro passageiro_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.passageiro
    ADD CONSTRAINT passageiro_pkey PRIMARY KEY (id);


--
-- TOC entry 4133 (class 2606 OID 808129)
-- Name: passageiro passageiro_unique_viagem_cpf; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.passageiro
    ADD CONSTRAINT passageiro_unique_viagem_cpf UNIQUE (viagem_id, cpf);


--
-- TOC entry 4144 (class 2606 OID 816806)
-- Name: status_pendencia status_pendencia_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.status_pendencia
    ADD CONSTRAINT status_pendencia_pkey PRIMARY KEY (status);


--
-- TOC entry 4146 (class 2606 OID 816813)
-- Name: tipo_entidade_pendencia tipo_entidade_pendencia_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.tipo_entidade_pendencia
    ADD CONSTRAINT tipo_entidade_pendencia_pkey PRIMARY KEY (tipo);


--
-- TOC entry 4129 (class 2606 OID 808069)
-- Name: viagem viagem_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_pkey PRIMARY KEY (id);


--
-- TOC entry 4123 (class 2606 OID 808007)
-- Name: viagem_tipo viagem_tipo_pkey; Type: CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem_tipo
    ADD CONSTRAINT viagem_tipo_pkey PRIMARY KEY (nome);


--
-- TOC entry 4089 (class 2606 OID 799656)
-- Name: papel papel_pkey; Type: CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.papel
    ADD CONSTRAINT papel_pkey PRIMARY KEY (nome);


--
-- TOC entry 4140 (class 2606 OID 816628)
-- Name: token_validacao_email token_validacao_email_pkey; Type: CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.token_validacao_email
    ADD CONSTRAINT token_validacao_email_pkey PRIMARY KEY (id);


--
-- TOC entry 4142 (class 2606 OID 816630)
-- Name: token_validacao_email token_validacao_email_token_key; Type: CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.token_validacao_email
    ADD CONSTRAINT token_validacao_email_token_key UNIQUE (token);


--
-- TOC entry 4094 (class 2606 OID 799680)
-- Name: usuario usuario_cpf_key; Type: CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.usuario
    ADD CONSTRAINT usuario_cpf_key UNIQUE (cpf);


--
-- TOC entry 4096 (class 2606 OID 799678)
-- Name: usuario usuario_email_key; Type: CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.usuario
    ADD CONSTRAINT usuario_email_key UNIQUE (email);


--
-- TOC entry 4098 (class 2606 OID 799676)
-- Name: usuario usuario_pkey; Type: CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.usuario
    ADD CONSTRAINT usuario_pkey PRIMARY KEY (id);


--
-- TOC entry 4103 (class 1259 OID 816934)
-- Name: documento_validade_idx; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX documento_validade_idx ON eventual.documento USING btree (validade);


--
-- TOC entry 4147 (class 1259 OID 816839)
-- Name: fluxo_pendencia_entidade_idx; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX fluxo_pendencia_entidade_idx ON eventual.fluxo_pendencia USING btree (entidade_tipo, entidade_id);


--
-- TOC entry 4148 (class 1259 OID 816840)
-- Name: fluxo_pendencia_latest_idx; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX fluxo_pendencia_latest_idx ON eventual.fluxo_pendencia USING btree (entidade_tipo, entidade_id, criado_em DESC);


--
-- TOC entry 4107 (class 1259 OID 832862)
-- Name: idx_doc_empresa_cnpj; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_doc_empresa_cnpj ON eventual.documento_empresa USING btree (empresa_cnpj);


--
-- TOC entry 4121 (class 1259 OID 832864)
-- Name: idx_doc_motorista_id; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_doc_motorista_id ON eventual.documento_motorista USING btree (motorista_id);


--
-- TOC entry 4110 (class 1259 OID 832865)
-- Name: idx_doc_usuario_id; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_doc_usuario_id ON eventual.documento_usuario USING btree (usuario_id);


--
-- TOC entry 4113 (class 1259 OID 832863)
-- Name: idx_doc_veiculo_placa; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_doc_veiculo_placa ON eventual.documento_veiculo USING btree (veiculo_placa);


--
-- TOC entry 4136 (class 1259 OID 832866)
-- Name: idx_doc_viagem_id; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_doc_viagem_id ON eventual.documento_viagem USING btree (viagem_id);


--
-- TOC entry 4104 (class 1259 OID 832861)
-- Name: idx_documento_fluxo_pendencia_id; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_documento_fluxo_pendencia_id ON eventual.documento USING btree (fluxo_pendencia_id);


--
-- TOC entry 4116 (class 1259 OID 832867)
-- Name: idx_motorista_empresa_cnpj; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_motorista_empresa_cnpj ON eventual.motorista USING btree (empresa_cnpj);


--
-- TOC entry 4124 (class 1259 OID 832857)
-- Name: idx_viagem_empresa_cnpj; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_viagem_empresa_cnpj ON eventual.viagem USING btree (empresa_cnpj);


--
-- TOC entry 4125 (class 1259 OID 832859)
-- Name: idx_viagem_motorista_aux_id; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_viagem_motorista_aux_id ON eventual.viagem USING btree (motorista_aux_id);


--
-- TOC entry 4126 (class 1259 OID 832858)
-- Name: idx_viagem_motorista_id; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_viagem_motorista_id ON eventual.viagem USING btree (motorista_id);


--
-- TOC entry 4127 (class 1259 OID 832860)
-- Name: idx_viagem_veiculo_placa; Type: INDEX; Schema: eventual; Owner: -
--

CREATE INDEX idx_viagem_veiculo_placa ON eventual.viagem USING btree (veiculo_placa);


--
-- TOC entry 4137 (class 1259 OID 816636)
-- Name: idx_token_validacao_email_token; Type: INDEX; Schema: web; Owner: -
--

CREATE INDEX idx_token_validacao_email_token ON web.token_validacao_email USING btree (token);


--
-- TOC entry 4138 (class 1259 OID 816637)
-- Name: idx_token_validacao_email_usuario; Type: INDEX; Schema: web; Owner: -
--

CREATE INDEX idx_token_validacao_email_usuario ON web.token_validacao_email USING btree (usuario_id);


--
-- TOC entry 4090 (class 1259 OID 799691)
-- Name: idx_usuario_email; Type: INDEX; Schema: web; Owner: -
--

CREATE INDEX idx_usuario_email ON web.usuario USING btree (email);


--
-- TOC entry 4091 (class 1259 OID 799692)
-- Name: idx_usuario_empresa_cnpj; Type: INDEX; Schema: web; Owner: -
--

CREATE INDEX idx_usuario_empresa_cnpj ON web.usuario USING btree (empresa_cnpj);


--
-- TOC entry 4092 (class 1259 OID 832868)
-- Name: idx_usuario_papel_nome; Type: INDEX; Schema: web; Owner: -
--

CREATE INDEX idx_usuario_papel_nome ON web.usuario USING btree (papel_nome);


--
-- TOC entry 4181 (class 2620 OID 832297)
-- Name: fluxo_pendencia trg_analista_obrigatorio; Type: TRIGGER; Schema: eventual; Owner: -
--

CREATE TRIGGER trg_analista_obrigatorio BEFORE INSERT OR UPDATE ON eventual.fluxo_pendencia FOR EACH ROW EXECUTE FUNCTION eventual.fn_analista_obrigatorio();


--
-- TOC entry 4182 (class 2620 OID 816844)
-- Name: fluxo_pendencia trg_evitar_status_repetido; Type: TRIGGER; Schema: eventual; Owner: -
--

CREATE TRIGGER trg_evitar_status_repetido BEFORE INSERT ON eventual.fluxo_pendencia FOR EACH ROW EXECUTE FUNCTION eventual.fn_evitar_status_repetido();


--
-- TOC entry 4183 (class 2620 OID 816936)
-- Name: fluxo_pendencia trg_motivo_obrigatorio; Type: TRIGGER; Schema: eventual; Owner: -
--

CREATE TRIGGER trg_motivo_obrigatorio BEFORE INSERT ON eventual.fluxo_pendencia FOR EACH ROW EXECUTE FUNCTION eventual.fn_motivo_obrigatorio();


--
-- TOC entry 4184 (class 2620 OID 816842)
-- Name: fluxo_pendencia trg_valida_entidade; Type: TRIGGER; Schema: eventual; Owner: -
--

CREATE TRIGGER trg_valida_entidade BEFORE INSERT ON eventual.fluxo_pendencia FOR EACH ROW EXECUTE FUNCTION eventual.fn_valida_entidade();


--
-- TOC entry 4180 (class 2620 OID 816854)
-- Name: usuario trg_usuario_normalizar_email; Type: TRIGGER; Schema: web; Owner: -
--

CREATE TRIGGER trg_usuario_normalizar_email BEFORE INSERT OR UPDATE OF email ON web.usuario FOR EACH ROW EXECUTE FUNCTION web.normalizar_email_usuario();


--
-- TOC entry 4153 (class 2606 OID 800019)
-- Name: documento documento_documento_tipo_nome_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento
    ADD CONSTRAINT documento_documento_tipo_nome_fkey FOREIGN KEY (documento_tipo_nome) REFERENCES eventual.documento_tipo(nome);


--
-- TOC entry 4156 (class 2606 OID 800036)
-- Name: documento_empresa documento_empresa_empresa_cnpj_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_empresa
    ADD CONSTRAINT documento_empresa_empresa_cnpj_fkey FOREIGN KEY (empresa_cnpj) REFERENCES geral.empresa(cnpj);


--
-- TOC entry 4155 (class 2606 OID 800031)
-- Name: documento_empresa documento_empresa_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_empresa
    ADD CONSTRAINT documento_empresa_id_fkey FOREIGN KEY (id) REFERENCES eventual.documento(id) ON DELETE CASCADE;


--
-- TOC entry 4154 (class 2606 OID 816929)
-- Name: documento documento_fluxo_pendencia_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento
    ADD CONSTRAINT documento_fluxo_pendencia_fkey FOREIGN KEY (fluxo_pendencia_id) REFERENCES eventual.fluxo_pendencia(id) ON DELETE SET NULL;


--
-- TOC entry 4163 (class 2606 OID 800216)
-- Name: documento_motorista documento_motorista_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_motorista
    ADD CONSTRAINT documento_motorista_id_fkey FOREIGN KEY (id) REFERENCES eventual.documento(id) ON DELETE CASCADE;


--
-- TOC entry 4164 (class 2606 OID 800221)
-- Name: documento_motorista documento_motorista_motorista_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_motorista
    ADD CONSTRAINT documento_motorista_motorista_id_fkey FOREIGN KEY (motorista_id) REFERENCES eventual.motorista(id);


--
-- TOC entry 4161 (class 2606 OID 800081)
-- Name: documento_tipo_vinculo documento_tipo_permissao_tipo_nome_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_tipo_vinculo
    ADD CONSTRAINT documento_tipo_permissao_tipo_nome_fkey FOREIGN KEY (tipo_nome) REFERENCES eventual.documento_tipo(nome);


--
-- TOC entry 4157 (class 2606 OID 800046)
-- Name: documento_usuario documento_usuario_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_usuario
    ADD CONSTRAINT documento_usuario_id_fkey FOREIGN KEY (id) REFERENCES eventual.documento(id) ON DELETE CASCADE;


--
-- TOC entry 4158 (class 2606 OID 800051)
-- Name: documento_usuario documento_usuario_usuario_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_usuario
    ADD CONSTRAINT documento_usuario_usuario_id_fkey FOREIGN KEY (usuario_id) REFERENCES web.usuario(id);


--
-- TOC entry 4159 (class 2606 OID 800063)
-- Name: documento_veiculo documento_veiculo_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_veiculo
    ADD CONSTRAINT documento_veiculo_id_fkey FOREIGN KEY (id) REFERENCES eventual.documento(id) ON DELETE CASCADE;


--
-- TOC entry 4160 (class 2606 OID 800068)
-- Name: documento_veiculo documento_veiculo_veiculo_placa_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_veiculo
    ADD CONSTRAINT documento_veiculo_veiculo_placa_fkey FOREIGN KEY (veiculo_placa) REFERENCES geral.veiculo(placa);


--
-- TOC entry 4174 (class 2606 OID 808155)
-- Name: documento_viagem documento_viagem_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_viagem
    ADD CONSTRAINT documento_viagem_id_fkey FOREIGN KEY (id) REFERENCES eventual.documento(id) ON DELETE CASCADE;


--
-- TOC entry 4175 (class 2606 OID 808160)
-- Name: documento_viagem documento_viagem_viagem_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.documento_viagem
    ADD CONSTRAINT documento_viagem_viagem_id_fkey FOREIGN KEY (viagem_id) REFERENCES eventual.viagem(id);


--
-- TOC entry 4177 (class 2606 OID 816834)
-- Name: fluxo_pendencia fluxo_pendencia_analista_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.fluxo_pendencia
    ADD CONSTRAINT fluxo_pendencia_analista_fkey FOREIGN KEY (analista) REFERENCES web.usuario(email);


--
-- TOC entry 4178 (class 2606 OID 816824)
-- Name: fluxo_pendencia fluxo_pendencia_entidade_tipo_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.fluxo_pendencia
    ADD CONSTRAINT fluxo_pendencia_entidade_tipo_fkey FOREIGN KEY (entidade_tipo) REFERENCES eventual.tipo_entidade_pendencia(tipo);


--
-- TOC entry 4179 (class 2606 OID 816829)
-- Name: fluxo_pendencia fluxo_pendencia_status_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.fluxo_pendencia
    ADD CONSTRAINT fluxo_pendencia_status_fkey FOREIGN KEY (status) REFERENCES eventual.status_pendencia(status);


--
-- TOC entry 4162 (class 2606 OID 800204)
-- Name: motorista motorista_empresa_cnpj_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.motorista
    ADD CONSTRAINT motorista_empresa_cnpj_fkey FOREIGN KEY (empresa_cnpj) REFERENCES geral.empresa(cnpj);


--
-- TOC entry 4173 (class 2606 OID 808130)
-- Name: passageiro passageiro_viagem_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.passageiro
    ADD CONSTRAINT passageiro_viagem_id_fkey FOREIGN KEY (viagem_id) REFERENCES eventual.viagem(id) ON DELETE CASCADE;


--
-- TOC entry 4172 (class 2606 OID 832831)
-- Name: viagem viagem_empresa_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_empresa_fkey FOREIGN KEY (empresa_cnpj) REFERENCES geral.empresa(cnpj);


--
-- TOC entry 4171 (class 2606 OID 808100)
-- Name: viagem viagem_motorista_aux_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_motorista_aux_id_fkey FOREIGN KEY (motorista_aux_id) REFERENCES eventual.motorista(id);


--
-- TOC entry 4170 (class 2606 OID 808095)
-- Name: viagem viagem_motorista_id_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_motorista_id_fkey FOREIGN KEY (motorista_id) REFERENCES eventual.motorista(id);


--
-- TOC entry 4167 (class 2606 OID 808080)
-- Name: viagem viagem_municipio_destino_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_municipio_destino_fkey FOREIGN KEY (municipio_destino) REFERENCES geral.municipio(nome);


--
-- TOC entry 4166 (class 2606 OID 808075)
-- Name: viagem viagem_municipio_origem_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_municipio_origem_fkey FOREIGN KEY (municipio_origem) REFERENCES geral.municipio(nome);


--
-- TOC entry 4165 (class 2606 OID 808070)
-- Name: viagem viagem_regiao_codigo_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_regiao_codigo_fkey FOREIGN KEY (regiao_codigo) REFERENCES geral.regiao(codigo);


--
-- TOC entry 4169 (class 2606 OID 808090)
-- Name: viagem viagem_veiculo_placa_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_veiculo_placa_fkey FOREIGN KEY (veiculo_placa) REFERENCES geral.veiculo(placa);


--
-- TOC entry 4168 (class 2606 OID 808085)
-- Name: viagem viagem_viagem_tipo_fkey; Type: FK CONSTRAINT; Schema: eventual; Owner: -
--

ALTER TABLE ONLY eventual.viagem
    ADD CONSTRAINT viagem_viagem_tipo_fkey FOREIGN KEY (viagem_tipo) REFERENCES eventual.viagem_tipo(nome);


--
-- TOC entry 4176 (class 2606 OID 816631)
-- Name: token_validacao_email token_validacao_email_usuario_id_fkey; Type: FK CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.token_validacao_email
    ADD CONSTRAINT token_validacao_email_usuario_id_fkey FOREIGN KEY (usuario_id) REFERENCES web.usuario(id) ON DELETE CASCADE;


--
-- TOC entry 4152 (class 2606 OID 799686)
-- Name: usuario usuario_empresa_cnpj_fkey; Type: FK CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.usuario
    ADD CONSTRAINT usuario_empresa_cnpj_fkey FOREIGN KEY (empresa_cnpj) REFERENCES geral.empresa(cnpj);


--
-- TOC entry 4151 (class 2606 OID 799681)
-- Name: usuario usuario_papel_nome_fkey; Type: FK CONSTRAINT; Schema: web; Owner: -
--

ALTER TABLE ONLY web.usuario
    ADD CONSTRAINT usuario_papel_nome_fkey FOREIGN KEY (papel_nome) REFERENCES web.papel(nome);


-- Completed on 2026-03-24 13:16:31

--
-- PostgreSQL database dump complete
--

