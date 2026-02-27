// Lightweight SQL syntax highlighter — no external dependencies
(function () {
    'use strict';

    const KEYWORDS = new Set([
        'ADD','ALL','ALTER','AND','ANY','AS','ASC','BACKUP','BETWEEN','BY','CASE','CHECK',
        'COLUMN','CONSTRAINT','CREATE','CROSS','DATABASE','DEFAULT','DELETE','DESC','DISTINCT',
        'DROP','ELSE','END','EXEC','EXISTS','FOREIGN','FROM','FULL','GROUP','HAVING','IN',
        'INDEX','INNER','INSERT','INTO','IS','JOIN','KEY','LEFT','LIKE','LIMIT','NOT','NULL',
        'ON','OR','ORDER','OUTER','PRIMARY','PROCEDURE','REPLACE','RIGHT','ROWNUM','SELECT',
        'SET','SOME','TABLE','TOP','TRUNCATE','UNION','UNIQUE','UPDATE','VALUES','VIEW',
        'WHERE','WITH','BEGIN','COMMIT','ROLLBACK','TRANSACTION','TRIGGER','FUNCTION',
        'RETURNS','RETURN','DECLARE','PRINT','RAISERROR','THROW','TRY','CATCH','IF','ELSE',
        'WHILE','BREAK','CONTINUE','GOTO','IDENTITY','NOCOUNT','OUTPUT','OVER','PARTITION',
        'ROW_NUMBER','RANK','DENSE_RANK','NTILE','LAG','LEAD','FIRST_VALUE','LAST_VALUE',
        'CONVERT','CAST','ISNULL','COALESCE','NULLIF','LEN','LTRIM','RTRIM','TRIM',
        'SUBSTRING','CHARINDEX','REPLACE','UPPER','LOWER','GETDATE','GETUTCDATE','NEWID',
        'COUNT','SUM','AVG','MIN','MAX','CONCAT','FORMAT','DATEDIFF','DATEADD','DATENAME',
        'DATEPART','YEAR','MONTH','DAY','INT','BIGINT','SMALLINT','TINYINT','BIT','DECIMAL',
        'NUMERIC','FLOAT','REAL','MONEY','SMALLMONEY','CHAR','VARCHAR','NCHAR','NVARCHAR',
        'TEXT','NTEXT','DATE','DATETIME','DATETIME2','SMALLDATETIME','TIME','UNIQUEIDENTIFIER',
        'VARBINARY','IMAGE','XML','SYSNAME','GO','USE','EXEC','EXECUTE','SP_EXECUTESQL',
        'SCHEMA','OBJECT_ID','OBJECT_NAME','SCOPE_IDENTITY','@@IDENTITY','@@ROWCOUNT',
        'NOLOCK','READUNCOMMITTED','READCOMMITTED','REPEATABLEREAD','SERIALIZABLE',
        'READ','WRITE','HOLD','TABLOCK','UPDLOCK','ROWLOCK','MERGE','WHEN','MATCHED',
        'THEN','INSERT','UPDATE','DELETE','USING','ENABLE','DISABLE','INSTEAD','OF','FOR',
        'AFTER','BEFORE','EACH','REFERENCING','NEW','OLD','GENERATED','ALWAYS',
        'ASC','DESC','NULLS','FIRST','LAST','UNBOUNDED','PRECEDING','FOLLOWING','CURRENT',
        'ROWS','RANGE','GROUPS','EXCLUDE','TIES','OTHERS','OTHERS'
    ]);

    function esc(s) {
        return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function highlightSQL(code) {
        const tokens = [];
        let i = 0;
        const n = code.length;

        while (i < n) {
            // Single-line comment: -- ...
            if (code[i] === '-' && i + 1 < n && code[i + 1] === '-') {
                let j = i;
                while (j < n && code[j] !== '\n') j++;
                tokens.push({ type: 'comment', value: code.slice(i, j) });
                i = j;
            }
            // Block comment: /* ... */
            else if (code[i] === '/' && i + 1 < n && code[i + 1] === '*') {
                let j = i + 2;
                while (j < n && !(code[j] === '*' && j + 1 < n && code[j + 1] === '/')) j++;
                j = Math.min(j + 2, n);
                tokens.push({ type: 'comment', value: code.slice(i, j) });
                i = j;
            }
            // Single-quoted string: '...'
            else if (code[i] === "'") {
                let j = i + 1;
                while (j < n) {
                    if (code[j] === "'" && j + 1 < n && code[j + 1] === "'") { j += 2; } // escaped ''
                    else if (code[j] === "'") { j++; break; }
                    else j++;
                }
                tokens.push({ type: 'string', value: code.slice(i, j) });
                i = j;
            }
            // N-prefixed string: N'...'
            else if ((code[i] === 'N' || code[i] === 'n') && i + 1 < n && code[i + 1] === "'") {
                let j = i + 2;
                while (j < n) {
                    if (code[j] === "'" && j + 1 < n && code[j + 1] === "'") { j += 2; }
                    else if (code[j] === "'") { j++; break; }
                    else j++;
                }
                tokens.push({ type: 'string', value: code.slice(i, j) });
                i = j;
            }
            // Bracketed identifier: [...]
            else if (code[i] === '[') {
                let j = i + 1;
                while (j < n && code[j] !== ']') j++;
                j = Math.min(j + 1, n);
                tokens.push({ type: 'ident-bracket', value: code.slice(i, j) });
                i = j;
            }
            // Number
            else if (/[0-9]/.test(code[i]) || (code[i] === '-' && i + 1 < n && /[0-9]/.test(code[i + 1]) && (i === 0 || /[\s(,=<>+\-*/]/.test(code[i - 1])))) {
                let j = i;
                if (code[j] === '-') j++;
                while (j < n && /[0-9._e]/.test(code[j])) j++;
                tokens.push({ type: 'number', value: code.slice(i, j) });
                i = j;
            }
            // Keyword or identifier
            else if (/[a-zA-Z_@#]/.test(code[i])) {
                let j = i;
                while (j < n && /[a-zA-Z0-9_@#$]/.test(code[j])) j++;
                const word = code.slice(i, j);
                const type = KEYWORDS.has(word.toUpperCase()) ? 'keyword' : 'ident';
                tokens.push({ type, value: word });
                i = j;
            }
            // Anything else (operators, whitespace, punctuation)
            else {
                tokens.push({ type: 'other', value: code[i] });
                i++;
            }
        }

        return tokens.map(t => {
            const v = esc(t.value);
            switch (t.type) {
                case 'keyword': return `<span class="sql-kw">${v}</span>`;
                case 'comment': return `<span class="sql-cm">${v}</span>`;
                case 'string': return `<span class="sql-st">${v}</span>`;
                case 'number': return `<span class="sql-nm">${v}</span>`;
                case 'ident-bracket': return `<span class="sql-id">${v}</span>`;
                default: return v;
            }
        }).join('');
    }

    window.sqlHighlight = {
        highlightElement: function (elementId) {
            const el = document.getElementById(elementId);
            if (!el) return;
            const raw = el.textContent || el.innerText || '';
            el.innerHTML = highlightSQL(raw);
        }
    };
})();
