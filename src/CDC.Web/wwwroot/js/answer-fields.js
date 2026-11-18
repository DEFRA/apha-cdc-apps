// What the panels that edit one answer at a time have in common: the per-species and the repeating questions of the Questions tab each
// show their answers in a table and edit one of them in a panel below it. This reads an answer from the panel's control for a field, writes
// one to it, and works out the text the table shows for it.
//
// A panel has an element marked data-answer-field for each field, with the field's data type in data-field-type and whether the user may
// change it in data-field-editable. Its control, or for a list of choices each of its checkboxes, is marked data-answer-editor. A text
// answer the user may not change is shown formatted in an element marked data-answer-text-view instead.
(function () {
    'use strict';

    // How many characters of a text answer the table shows, as the legacy table did.
    var SUMMARY_LENGTH = 50;

    // The named character references the server decodes when it works out the same text, which are the ones HTML 4 defines and apos. The names
    // for U+00A0 to U+00FF come first, in order of code point, and the others follow, each as its name and its code point in hexadecimal.
    var LATIN1_NAMES = [
        'nbsp iexcl cent pound curren yen brvbar sect uml copy ordf laquo not shy reg macr deg plusmn',
        'sup2 sup3 acute micro para middot cedil sup1 ordm raquo frac14 frac12 frac34 iquest Agrave',
        'Aacute Acirc Atilde Auml Aring AElig Ccedil Egrave Eacute Ecirc Euml Igrave Iacute Icirc Iuml',
        'ETH Ntilde Ograve Oacute Ocirc Otilde Ouml times Oslash Ugrave Uacute Ucirc Uuml Yacute THORN',
        'szlig agrave aacute acirc atilde auml aring aelig ccedil egrave eacute ecirc euml igrave iacute',
        'icirc iuml eth ntilde ograve oacute ocirc otilde ouml divide oslash ugrave uacute ucirc uuml',
        'yacute thorn yuml'
    ].join(' ');
    var OTHER_REFERENCES = [
        'quot:22 amp:26 apos:27 lt:3c gt:3e OElig:152 oelig:153 Scaron:160 scaron:161 Yuml:178 fnof:192',
        'circ:2c6 tilde:2dc Alpha:391 Beta:392 Gamma:393 Delta:394 Epsilon:395 Zeta:396 Eta:397 Theta:398',
        'Iota:399 Kappa:39a Lambda:39b Mu:39c Nu:39d Xi:39e Omicron:39f Pi:3a0 Rho:3a1 Sigma:3a3 Tau:3a4',
        'Upsilon:3a5 Phi:3a6 Chi:3a7 Psi:3a8 Omega:3a9 alpha:3b1 beta:3b2 gamma:3b3 delta:3b4 epsilon:3b5',
        'zeta:3b6 eta:3b7 theta:3b8 iota:3b9 kappa:3ba lambda:3bb mu:3bc nu:3bd xi:3be omicron:3bf pi:3c0',
        'rho:3c1 sigmaf:3c2 sigma:3c3 tau:3c4 upsilon:3c5 phi:3c6 chi:3c7 psi:3c8 omega:3c9 thetasym:3d1',
        'upsih:3d2 piv:3d6 ensp:2002 emsp:2003 thinsp:2009 zwnj:200c zwj:200d lrm:200e rlm:200f',
        'ndash:2013 mdash:2014 lsquo:2018 rsquo:2019 sbquo:201a ldquo:201c rdquo:201d bdquo:201e',
        'dagger:2020 Dagger:2021 bull:2022 hellip:2026 permil:2030 prime:2032 Prime:2033 lsaquo:2039',
        'rsaquo:203a oline:203e frasl:2044 euro:20ac image:2111 weierp:2118 real:211c trade:2122',
        'alefsym:2135 larr:2190 uarr:2191 rarr:2192 darr:2193 harr:2194 crarr:21b5 lArr:21d0 uArr:21d1',
        'rArr:21d2 dArr:21d3 hArr:21d4 forall:2200 part:2202 exist:2203 empty:2205 nabla:2207 isin:2208',
        'notin:2209 ni:220b prod:220f sum:2211 minus:2212 lowast:2217 radic:221a prop:221d infin:221e',
        'ang:2220 and:2227 or:2228 cap:2229 cup:222a int:222b there4:2234 sim:223c cong:2245 asymp:2248',
        'ne:2260 equiv:2261 le:2264 ge:2265 sub:2282 sup:2283 nsub:2284 sube:2286 supe:2287 oplus:2295',
        'otimes:2297 perp:22a5 sdot:22c5 lceil:2308 rceil:2309 lfloor:230a rfloor:230b lang:2329',
        'rang:232a loz:25ca spades:2660 clubs:2663 hearts:2665 diams:2666'
    ].join(' ');

    var namedReferences = new Map();

    LATIN1_NAMES.split(' ').forEach(function (name, index) {
        namedReferences.set(name, 0xa0 + index);
    });
    OTHER_REFERENCES.split(' ').forEach(function (reference) {
        var parts = reference.split(':');

        namedReferences.set(parts[0], Number.parseInt(parts[1], 16));
    });

    // Whether a number in a character reference is one the server decodes: not zero, not a surrogate and not past the last code point.
    function isCharacter(code) {
        return code > 0 && code <= 0x10ffff && (code < 0xd800 || code > 0xdfff);
    }

    // The character a reference stands for, or the reference as it was written when it is not one the server decodes.
    function decodeReference(reference, decimal, hexadecimal, name) {
        var code;

        if (name) {
            code = namedReferences.get(name);
        } else {
            code = decimal ? Number.parseInt(decimal, 10) : Number.parseInt(hexadecimal, 16);
        }

        return isCharacter(code) ? String.fromCodePoint(code) : reference;
    }

    // Removes every tag, which runs from a < to the next >, as the server does; a < with no > after it is not a tag. The brackets are searched for
    // rather than matched with a regular expression, which is slow on a long text that has many < and no >.
    function withoutTags(html) {
        var lastClose = html.lastIndexOf('>');
        var text = '';
        var from = 0;
        var open = html.indexOf('<');

        while (open !== -1 && open < lastClose) {
            text += html.slice(from, open);
            from = html.indexOf('>', open) + 1;
            open = html.indexOf('<', from);
        }

        return text + html.slice(from);
    }

    // The text of an answer without its markup, as the server works it out: the tags are removed, then the character references are decoded,
    // once, and the ends trimmed. Only the string is worked on and nothing is parsed as HTML, so no markup in an answer is turned into
    // elements or run.
    function plainText(html) {
        return withoutTags(html).replace(/&(?:#(\d+)|#x([0-9a-f]+)|([a-z][a-z0-9]*));/gi, decodeReference).trim();
    }

    function labelOf(control) {
        var label = document.querySelector('label[for="' + control.id + '"]');

        return label ? label.textContent.trim() : '';
    }

    function dataType(wrapper) {
        return wrapper.dataset.fieldType;
    }

    function booleanText(value) {
        if (value === 'true') {
            return 'Yes';
        }

        return value === 'false' ? 'No' : '';
    }

    // The text the table shows for an answer, which the server works out in the same way for the answers it renders.
    function summaryText(wrapper, value) {
        if (value === '') {
            return '';
        }

        switch (dataType(wrapper)) {
            case 'Boolean':
                return booleanText(value);
            case 'List':
                var option = wrapper.querySelector('option[value="' + value + '"]');
                return option ? option.textContent : '';
            case 'MultiValueList':
                var chosen = new Set(value.split(','));
                return Array.prototype.filter.call(wrapper.querySelectorAll('input[type="checkbox"]'), function (box) {
                    return chosen.has(box.value);
                }).map(labelOf).join(', ');
            case 'Decimal':
                return Number.isFinite(Number(value)) ? Number(value).toFixed(2) : value;
            case 'Date':
                return value;
            default:
                return plainText(value).slice(0, SUMMARY_LENGTH);
        }
    }

    function readControl(wrapper) {
        if (dataType(wrapper) === 'MultiValueList') {
            return Array.prototype.filter.call(wrapper.querySelectorAll('input[type="checkbox"]'), function (box) {
                return box.checked;
            }).map(function (box) {
                return box.value;
            }).join(',');
        }

        var control = wrapper.querySelector('[data-answer-editor]');

        return control ? control.value : '';
    }

    function writeControl(wrapper, value) {
        if (dataType(wrapper) === 'MultiValueList') {
            var chosen = new Set(value === '' ? [] : value.split(','));
            wrapper.querySelectorAll('input[type="checkbox"]').forEach(function (box) {
                box.checked = chosen.has(box.value);
            });
            return;
        }

        var control = wrapper.querySelector('[data-answer-editor]');

        if (control) {
            control.value = value;

            // A drop-down that decides whether other questions apply is put back to this answer when a change to it is not confirmed.
            if ('relevanceAffects' in control.dataset) {
                control.dataset.relevanceHeld = control.value;
            }

            // A rich text field shows an editor in place of its text area, which has to be filled as well.
            var editor = document.getElementById(control.id + '-editor');
            if (editor && window.appRichText) {
                window.appRichText.fill(editor, value);
            }
            return;
        }

        var view = wrapper.querySelector('[data-answer-text-view]');

        if (view && window.appRichText) {
            window.appRichText.fill(view, value);
        } else if (view) {
            view.textContent = plainText(value);
        }
    }

    window.appAnswerFields = {
        fieldId: function (wrapper) {
            return wrapper.dataset.answerField;
        },
        isEditable: function (wrapper) {
            return wrapper.dataset.fieldEditable === 'true';
        },
        summaryText: summaryText,
        read: readControl,
        write: writeControl
    };
})();
