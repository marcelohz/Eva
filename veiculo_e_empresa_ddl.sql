-- geral.empresa definition

-- Drop table

-- DROP TABLE geral.empresa;

CREATE TABLE geral.empresa (
	cnpj text NOT NULL,
	nome text NOT NULL,
	telefone varchar NULL,
	fax varchar NULL,
	cep text NULL,
	email text NULL,
	regiao_codigo text NULL,
	processo text NULL,
	endereco varchar NULL,
	municipio_nome text NULL,
	nome_simplificado text NULL,
	data_inicio_operacao date NULL,
	data_fim_operacao date NULL,
	inscricao_estadual text NULL,
	garagem_telefone varchar NULL,
	garagem_cep varchar NULL,
	observacoes text NULL,
	data_inclusao_metroplan date NULL,
	garagem_endereco varchar NULL,
	data_inclusao date DEFAULT now() NULL,
	data_exclusao date NULL,
	telefone2 text NULL,
	procurador text NULL,
	procurador_endereco text NULL,
	procurador_telefone text NULL,
	procurador_email text NULL,
	data_entrega_documentacao date NULL,
	eh_acordo bool DEFAULT false NOT NULL,
	nome_fantasia text NULL,
	endereco_numero text NULL,
	endereco_complemento text NULL,
	bairro text NULL,
	cidade text NULL,
	estado text NULL,
	celular text NULL,
	data_inclusao_eventual date NULL,
	CONSTRAINT empresa_fax_valido_chk CHECK (((fax IS NULL) OR ((fax)::text ~ '^[0-9]{10}$'::text))),
	CONSTRAINT empresa_pkey PRIMARY KEY (cnpj)
);

-- Table Triggers

create trigger trg_registra_historico_empresa after
update
    on
    geral.empresa for each row execute function concessao.trigger_registra_historico_por_empresa();
create trigger upper_empresa before
insert
    or
update
    on
    geral.empresa for each row execute function geral.upper_empresa();


-- geral.empresa foreign keys

ALTER TABLE geral.empresa ADD CONSTRAINT contratado_regiao_codigo_fkey FOREIGN KEY (regiao_codigo) REFERENCES geral.regiao(codigo) ON UPDATE CASCADE;
ALTER TABLE geral.empresa ADD CONSTRAINT empresa_municipio_fkey FOREIGN KEY (municipio_nome) REFERENCES geral.municipio(nome) ON UPDATE CASCADE;




-- geral.veiculo definition

-- Drop table

-- DROP TABLE geral.veiculo;

CREATE TABLE geral.veiculo (
	placa text NOT NULL,
	prefixo int4 NULL,
	renavan text NULL,
	potencia_motor int4 NULL,
	numero_portas int4 NULL,
	tem_ar_condicionado bool NULL,
	tem_poltrona_reclinavel bool NULL,
	chassi_ano int4 NULL,
	carroceria_ano int4 NULL,
	veiculo_qualidade_nome text NULL,
	veiculo_motor_nome text NULL,
	chassi_numero text NULL,
	empresa_codigo_codigo text NULL,
	veiculo_chassi_nome text NULL,
	veiculo_carroceria_nome text NULL,
	acordo_codigo text NULL,
	cor_principal_nome text NULL,
	cor_secundaria_nome text NULL,
	veiculo_combustivel_nome text NULL,
	tem_assento_cobrador bool NULL,
	tem_catraca bool NULL,
	numero_lugares int4 NULL,
	empresa_cnpj text NULL,
	ano_fabricacao int4 NULL,
	modelo text NULL,
	ativo bool NULL,
	data_inclusao_concessao date NULL,
	data_exclusao_concessao date NULL,
	veiculo_rodados_nome text NULL,
	tem_elevador bool NULL,
	numero_inclusao text NULL,
	numero_exclusao text NULL,
	validador_be_numero text NULL,
	observacoes text NULL,
	processo_exclusao text NULL,
	processo_inclusao text NULL,
	classificacao_inmetro_nome text NULL,
	data_inclusao_fretamento date NULL,
	data_exclusao_fretamento date NULL,
	data_inicio_seguro date NULL,
	data_vencimento_seguro date NULL,
	concessao_veiculo_tipo_nome text NULL,
	fretamento_veiculo_tipo_nome text NULL,
	comodato bool DEFAULT false NULL,
	apolice text NULL,
	seguradora text NULL,
	crlv int4 NULL,
	inativo bool DEFAULT false NOT NULL,
	modelo_ano int4 NULL,
	data_inclusao_eventual date NULL,
	CONSTRAINT codigo_ou_cnpj CHECK (((empresa_codigo_codigo IS NOT NULL) OR (empresa_cnpj IS NOT NULL))),
	CONSTRAINT placa_valida_chk CHECK ((placa ~ '[A-Z]{3}[0-9]([A-Z0-9])[0-9][0-9]'::text)),
	CONSTRAINT veiculo_pkey PRIMARY KEY (placa)
);
CREATE INDEX veiculo_inclusao_fret_idx ON geral.veiculo USING btree (data_inclusao_fretamento);
CREATE INDEX veiculo_seguro_idx ON geral.veiculo USING btree (data_vencimento_seguro);

-- Table Triggers

create trigger upper_veiculo before
insert
    or
update
    on
    geral.veiculo for each row execute function geral.upper_veiculo();


-- geral.veiculo foreign keys

ALTER TABLE geral.veiculo ADD CONSTRAINT empresa_codigo_codigo_fkey FOREIGN KEY (empresa_codigo_codigo) REFERENCES geral.empresa_codigo(codigo);
ALTER TABLE geral.veiculo ADD CONSTRAINT validador_be_numero_fkey FOREIGN KEY (validador_be_numero) REFERENCES geral.validador_be(numero);
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_acordo_codigo_fkey FOREIGN KEY (acordo_codigo) REFERENCES geral.empresa_codigo(codigo);
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_carroceria_nome_fkey FOREIGN KEY (veiculo_carroceria_nome) REFERENCES geral.veiculo_carroceria(nome) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_chassi_nome_fkey FOREIGN KEY (veiculo_chassi_nome) REFERENCES geral.veiculo_chassi(nome) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_classificacao_inmetro_nome_fkey FOREIGN KEY (classificacao_inmetro_nome) REFERENCES geral.classificacao_inmetro(nome);
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_combustivel_nome_fkey FOREIGN KEY (veiculo_combustivel_nome) REFERENCES geral.veiculo_combustivel(nome) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_concessao_veiculo_tipo_nome_fkey FOREIGN KEY (concessao_veiculo_tipo_nome) REFERENCES concessao.concessao_veiculo_tipo(nome);
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_cor1_fkey FOREIGN KEY (cor_principal_nome) REFERENCES geral.cor(nome) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_cor2_fkey FOREIGN KEY (cor_secundaria_nome) REFERENCES geral.cor(nome) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_empresa_cnpj_fkey FOREIGN KEY (empresa_cnpj) REFERENCES geral.empresa(cnpj) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_fretamento_veiculo_tipo_nome_fkey FOREIGN KEY (fretamento_veiculo_tipo_nome) REFERENCES fretamento.fretamento_veiculo_tipo(nome) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_seguradora_fk FOREIGN KEY (seguradora) REFERENCES fretamento.seguradora(nome);
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_veiculo_motor_fkey FOREIGN KEY (veiculo_motor_nome) REFERENCES geral.veiculo_motor(nome) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_veiculo_qualidade_fkey FOREIGN KEY (veiculo_qualidade_nome) REFERENCES geral.veiculo_qualidade(nome) ON UPDATE CASCADE;
ALTER TABLE geral.veiculo ADD CONSTRAINT veiculo_veiculo_rodados_fkey FOREIGN KEY (veiculo_rodados_nome) REFERENCES geral.veiculo_rodados(nome);