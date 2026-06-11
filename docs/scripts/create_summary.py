#!/usr/bin/env python3
"""
Create a summary report of the test updates
"""

import csv

def create_summary():
    """Create summary of test categorization"""
    input_file = 'Cahier_Recette_Avec_Resultats_Updated.csv'
    
    stats = {
        'total': 0,
        'unit_only': 0,
        'functional_only': 0,
        'both': 0,
        'by_criticite': {},
        'by_process': {}
    }
    
    with open(input_file, 'r', encoding='utf-8') as f:
        reader = csv.reader(f, delimiter=';')
        next(reader)  # Skip header
        
        for row in reader:
            if len(row) < 13:
                continue
            
            process = row[0]
            criticite = row[8]
            test_unitaire = row[10]
            test_fonctionnel = row[11]
            
            stats['total'] += 1
            
            # Count by test type
            if test_unitaire == 'Oui' and test_fonctionnel == 'Oui':
                stats['both'] += 1
            elif test_unitaire == 'Oui':
                stats['unit_only'] += 1
            elif test_fonctionnel == 'Oui':
                stats['functional_only'] += 1
            
            # Count by criticité
            stats['by_criticite'][criticite] = stats['by_criticite'].get(criticite, 0) + 1
            
            # Count by process
            stats['by_process'][process] = stats['by_process'].get(process, 0) + 1
    
    # Print summary
    print("=" * 70)
    print("RÉSUMÉ DE LA MISE À JOUR DU CAHIER DE RECETTE")
    print("=" * 70)
    print(f"\n📊 Total de tests traités: {stats['total']}")
    print(f"\n🔍 Répartition par type de test:")
    print(f"   • Tests unitaires ET fonctionnels: {stats['both']} ({stats['both']*100//stats['total']}%)")
    print(f"   • Tests unitaires uniquement: {stats['unit_only']} ({stats['unit_only']*100//stats['total']}%)")
    print(f"   • Tests fonctionnels uniquement: {stats['functional_only']} ({stats['functional_only']*100//stats['total']}%)")
    
    print(f"\n⚠️  Répartition par criticité:")
    for crit, count in sorted(stats['by_criticite'].items()):
        print(f"   • {crit}: {count} tests")
    
    print(f"\n📦 Répartition par processus:")
    for proc, count in sorted(stats['by_process'].items(), key=lambda x: x[1], reverse=True):
        print(f"   • {proc}: {count} tests")
    
    print("\n" + "=" * 70)
    print("✅ Fichiers générés:")
    print("   • Cahier_Recette_Avec_Resultats_Updated.csv")
    print("\n📋 Structure des colonnes:")
    print("   K: Commentaires (contexte et type de test)")
    print("   L: Test Unitaire (Oui/Non)")
    print("   M: Test Fonctionnel (Oui/Non)")
    print("   N: Procédure de réalisation (étapes détaillées)")
    print("=" * 70)

if __name__ == '__main__':
    create_summary()
