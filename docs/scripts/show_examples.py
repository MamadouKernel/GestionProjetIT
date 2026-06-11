#!/usr/bin/env python3
"""
Show examples of the updated test cases
"""

import csv

def show_examples():
    """Display examples of different test types"""
    input_file = 'Cahier_Recette_Avec_Resultats_Updated.csv'
    
    examples = {
        'Sécurité': [],
        'Fonctionnel UI': [],
        'Logique métier': [],
        'Mixte': []
    }
    
    with open(input_file, 'r', encoding='utf-8') as f:
        reader = csv.reader(f, delimiter=';')
        next(reader)  # Skip header
        
        for row in reader:
            if len(row) < 13:
                continue
            
            test_id = row[1]
            point_verifier = row[4]
            commentaire = row[9]
            test_unitaire = row[10]
            test_fonctionnel = row[11]
            procedure = row[12]
            
            example = {
                'id': test_id,
                'point': point_verifier,
                'commentaire': commentaire,
                'unit': test_unitaire,
                'func': test_fonctionnel,
                'proc': procedure[:100] + '...' if len(procedure) > 100 else procedure
            }
            
            if 'sécurité' in commentaire.lower():
                if len(examples['Sécurité']) < 2:
                    examples['Sécurité'].append(example)
            elif test_unitaire == 'Oui' and test_fonctionnel == 'Oui':
                if len(examples['Mixte']) < 2:
                    examples['Mixte'].append(example)
            elif test_fonctionnel == 'Oui' and test_unitaire == 'Non':
                if len(examples['Fonctionnel UI']) < 2:
                    examples['Fonctionnel UI'].append(example)
            elif test_unitaire == 'Oui' and test_fonctionnel == 'Non':
                if len(examples['Logique métier']) < 2:
                    examples['Logique métier'].append(example)
    
    print("\n" + "=" * 100)
    print("EXEMPLES DE TESTS PAR CATÉGORIE")
    print("=" * 100)
    
    for category, tests in examples.items():
        if tests:
            print(f"\n🔹 {category.upper()}")
            print("-" * 100)
            for test in tests:
                print(f"\n  ID: {test['id']}")
                print(f"  Point à vérifier: {test['point']}")
                print(f"  Commentaire (K): {test['commentaire']}")
                print(f"  Test Unitaire (L): {test['unit']}")
                print(f"  Test Fonctionnel (M): {test['func']}")
                print(f"  Procédure (N): {test['proc']}")
    
    print("\n" + "=" * 100)

if __name__ == '__main__':
    show_examples()
